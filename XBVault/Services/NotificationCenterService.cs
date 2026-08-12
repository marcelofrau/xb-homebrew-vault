using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using XBVault.Models;

namespace XBVault.Services;

public class NotificationCenterService
{
    public const int MaxVisibleToasts = 4;
    public const int MaxHistory = 50;
    public static readonly TimeSpan DefaultAutoDismiss = TimeSpan.FromSeconds(6);
    public const string DefaultIconUri = "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-info-20.png";

    private readonly object _gate = new();
    private readonly ObservableCollection<NotificationItem> _history = [];
    private readonly Dictionary<Guid, Timer> _dismissTimers = [];

    public ObservableCollection<NotificationItem> Active { get; } = [];

    public event EventHandler<NotificationItem>? NotificationAdded;
    public event EventHandler<NotificationItem>? NotificationDismissed;
    public event Action? UnacknowledgedChanged;

    public int UnacknowledgedCount { get; private set; }

    public IReadOnlyList<NotificationItem> History => _history;

    public NotificationItem Notify(string title, string message, string? iconUri = null, Action? clickAction = null)
    {
        var item = new NotificationItem
        {
            Title = title,
            Message = message,
            IconUri = iconUri ?? DefaultIconUri,
            ClickAction = clickAction
        };
        Logger.Trace($"NotificationCenter: Notify '{title}' — {message}");
        AddActive(item);
        return item;
    }

    public NotificationItem NotifyGrouped(string title, IReadOnlyList<NotificationAction> items, bool autoDismiss = true)
    {
        var item = new NotificationItem
        {
            Title = title,
            Message = $"{items.Count} notification{(items.Count == 1 ? string.Empty : "s")}",
            IconUri = DefaultIconUri,
            ClickAction = null,
            Actions = items
        };
        Logger.Trace($"NotificationCenter: NotifyGrouped '{title}' ({items.Count} actions):");
        foreach (var a in items)
            Logger.Trace($"  → {a.Label}");
        AddActive(item, autoDismiss);
        return item;
    }

    public NotificationItem NotifyGroupedReplacing(string tag, string title, IReadOnlyList<NotificationAction> items, bool autoDismiss = true)
    {
        var item = new NotificationItem
        {
            Tag = tag,
            Title = title,
            Message = $"{items.Count} notification{(items.Count == 1 ? string.Empty : "s")}",
            IconUri = DefaultIconUri,
            ClickAction = null,
            Actions = items
        };
        Logger.Trace($"NotificationCenter: NotifyGroupedReplacing '{title}' (tag '{tag}', {items.Count} actions):");
        foreach (var a in items)
            Logger.Trace($"  → {a.Label}");
        ReplaceActive(item, autoDismiss);
        return item;
    }

    private void ReplaceActive(NotificationItem item, bool autoDismiss = true)
    {
        PostToUi(() =>
        {
            var existing = Active.FirstOrDefault(n => !string.IsNullOrEmpty(n.Tag) && n.Tag == item.Tag);
            if (existing is not null)
            {
                StopDismissTimer(existing.Id);
                Active.Remove(existing);
                AdjustUnacknowledged(-1);
                Logger.Trace($"NotificationCenter: replaced active '{existing.Title}' with '{item.Title}' (tag '{item.Tag}')");
            }
            var oldHistory = _history.Where(n => !string.IsNullOrEmpty(n.Tag) && n.Tag == item.Tag).ToList();
            foreach (var old in oldHistory)
                _history.Remove(old);
        });
        AddActive(item, autoDismiss);
    }

    private void AddActive(NotificationItem item, bool autoDismiss = true)
    {
        AdjustUnacknowledged(1);
        PostToUi(() =>
        {
            Active.Add(item);
            while (Active.Count > MaxVisibleToasts)
                Complete(Active[0]);
            NotificationAdded?.Invoke(this, item);
        });
        if (autoDismiss)
            StartAutoDismiss(item);
    }

    public void Dismiss(Guid id)
    {
        PostToUi(() =>
        {
            var item = Active.FirstOrDefault(n => n.Id == id);
            if (item is not null)
                Complete(item);
        });
    }

    public void RemoveFromHistory(Guid id)
    {
        PostToUi(() =>
        {
            var item = _history.FirstOrDefault(n => n.Id == id);
            if (item is not null)
                _history.Remove(item);
        });
    }

    public void ClearAll()
    {
        PostToUi(() =>
        {
            foreach (var item in Active.ToList())
                Complete(item);
            _history.Clear();
        });
    }

    public void InvokeAction(NotificationItem item)
    {
        var action = item.ClickAction;
        if (action is not null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "NotificationCenterService: notification action threw");
            }
        }
        Dismiss(item.Id);
    }

    private void Complete(NotificationItem item)
    {
        StopDismissTimer(item.Id);
        Active.Remove(item);
        MoveToHistory(item);
        AdjustUnacknowledged(-1);
        NotificationDismissed?.Invoke(this, item);
    }

    private void MoveToHistory(NotificationItem item)
    {
        lock (_gate)
        {
            _history.Insert(0, item);
            while (_history.Count > MaxHistory)
                _history.RemoveAt(_history.Count - 1);
        }
    }

    private void StartAutoDismiss(NotificationItem item)
    {
        var timer = new Timer(
            _ => AutoDismiss(item),
            null,
            DefaultAutoDismiss,
            Timeout.InfiniteTimeSpan);
        lock (_gate)
        {
            _dismissTimers[item.Id] = timer;
        }
    }

    private void AutoDismiss(NotificationItem item)
    {
        PostToUi(() =>
        {
            if (Active.Contains(item))
                Complete(item);
        });
    }

    private void StopDismissTimer(Guid id)
    {
        lock (_gate)
        {
            if (_dismissTimers.Remove(id, out var timer))
                timer.Dispose();
        }
    }

    private void AdjustUnacknowledged(int delta)
    {
        lock (_gate)
        {
            UnacknowledgedCount = Math.Max(0, UnacknowledgedCount + delta);
        }
        PostToUi(() => UnacknowledgedChanged?.Invoke());
    }

    protected virtual void PostToUi(Action action)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            Logger.Debug($"NotificationCenterService: UI dispatch failed: {ex.Message}");
        }
    }
}
