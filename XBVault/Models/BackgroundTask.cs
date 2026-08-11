using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XBVault.Models;

public enum BackgroundTaskStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum BackgroundTaskKind
{
    OneShot,
    Job
}

public partial class BackgroundTask : ObservableObject
{
    private readonly Action<Action> _marshal;

    internal BackgroundTask(string name, BackgroundTaskKind kind, bool canCancel, string? jobKey, Action<Action> marshal)
    {
        Name = name;
        Kind = kind;
        CanCancel = canCancel;
        JobKey = jobKey;
        _marshal = marshal;
    }

    internal event Action<BackgroundTask>? Mutated;

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public BackgroundTaskKind Kind { get; }
    public string? JobKey { get; }
    public bool CanCancel { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    private BackgroundTaskStatus _status = BackgroundTaskStatus.Queued;
    public BackgroundTaskStatus Status
    {
        get => _status;
        internal set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsFinished));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(ShowProgress));
            }
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        internal set => SetProperty(ref _progress, value);
    }

    private bool _isIndeterminate = true;
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        internal set => SetProperty(ref _isIndeterminate, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        internal set => SetProperty(ref _statusMessage, value);
    }

    private DateTime? _startedAt;
    public DateTime? StartedAt
    {
        get => _startedAt;
        internal set => SetProperty(ref _startedAt, value);
    }

    private DateTime? _completedAt;
    public DateTime? CompletedAt
    {
        get => _completedAt;
        internal set => SetProperty(ref _completedAt, value);
    }

    private DateTime? _nextRunAt;
    public DateTime? NextRunAt
    {
        get => _nextRunAt;
        internal set
        {
            if (SetProperty(ref _nextRunAt, value))
                OnPropertyChanged(nameof(NextRunAtLocal));
        }
    }

    public DateTime? NextRunAtLocal => NextRunAt?.ToLocalTime();

    public ObservableCollection<string> Details { get; } = [];

    public bool IsRunning => Status == BackgroundTaskStatus.Running;
    public bool IsFinished => Status is BackgroundTaskStatus.Succeeded or BackgroundTaskStatus.Failed or BackgroundTaskStatus.Cancelled;
    public bool IsFailed => Status == BackgroundTaskStatus.Failed;
    public bool ShowProgress => Status == BackgroundTaskStatus.Running;

    public TimeSpan Elapsed
    {
        get
        {
            if (CompletedAt is { } completedAt) return completedAt - CreatedAt;
            return DateTime.UtcNow - CreatedAt;
        }
    }

    internal void SetRunning()
    {
        Mutate(() =>
        {
            StartedAt = DateTime.UtcNow;
            Status = BackgroundTaskStatus.Running;
        });
    }

    internal void Complete(BackgroundTaskStatus final, string message)
    {
        Mutate(() =>
        {
            Status = final;
            StatusMessage = message;
            CompletedAt = DateTime.UtcNow;
        });
    }

    public void ReportProgress(double progress)
    {
        Mutate(() => Progress = Math.Clamp(progress, 0, 1));
    }

    public void ReportStatus(string message)
    {
        Mutate(() => StatusMessage = message);
    }

    public void SetIndeterminate(bool indeterminate)
    {
        Mutate(() => IsIndeterminate = indeterminate);
    }

    public void AppendDetail(string line)
    {
        Mutate(() => Details.Add(line));
    }

    internal void NotifyElapsedChanged()
    {
        Mutate(() => OnPropertyChanged(nameof(Elapsed)));
    }

    internal void SetNextRun(DateTime? when)
    {
        Mutate(() => NextRunAt = when);
    }

    private void Mutate(Action action)
    {
        _marshal(() =>
        {
            action();
            Mutated?.Invoke(this);
        });
    }
}
