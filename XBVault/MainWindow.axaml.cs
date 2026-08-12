using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XBVault.Helpers;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;
using XBVault.Views;

namespace XBVault;

public partial class MainWindow : Window
{
    private const int TabCount = 7;
    private static readonly TimeSpan PopupFadeDuration = TimeSpan.FromMilliseconds(150);
    private const int ToastFadeOutMs = 250;
    private readonly ObservableCollection<ToastHost> _toastHosts = [];
    private NotificationCenterService? _notificationCenter;
    private TaskCenterViewModel? _taskCenter;
    private int _tasksFadeGen;
    private int _notificationsFadeGen;

    public MainWindow()
    {
        InitializeComponent();
        ToastItemsControl.ItemsSource = _toastHosts;
        MinWidth = WindowSettingsService.MinMainWindowWidth;
        MinHeight = WindowSettingsService.MinMainWindowHeight;
        var size = WindowSettingsService.GetMainWindowSize();
        Width = size.Width;
        Height = size.Height;
        VersionText.Text = BuildInfo.DisplayVersion;
        UpdateWindowStateIcons();
        AddHandler(InputElement.KeyDownEvent, OnMainWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        TasksPopup.Opened += OnTasksPopupOpened;
        TasksPopup.Closed += OnTasksPopupClosed;
        NotificationsPopup.Opened += OnNotificationsPopupOpened;
        NotificationsPopup.Closed += OnNotificationsPopupClosed;
        Opened += (_, _) => ApplyUiScale();
    }

    public void ApplyUiScale() => WindowFitHelper.ApplyScale(this, SettingsService.Current.UiScale);

    public bool IsModalDimmed
    {
        get => ModalDimOverlay.Opacity > 0.5;
        set
        {
            ModalDimOverlay.Opacity = value ? 1.0 : 0.0;
            ModalDimOverlay.IsHitTestVisible = value;
        }
    }

    public void BindNotifications(NotificationCenterService notificationCenter)
    {
        Logger.Trace("Flyout: BindNotifications called");
        _notificationCenter = notificationCenter;
        foreach (var item in notificationCenter.Active)
            _toastHosts.Add(new ToastHost(item));
        notificationCenter.Active.CollectionChanged += OnActiveNotificationsChanged;
        NotificationsPopup.DataContext = notificationCenter;
        NotificationsPanelHost.CloseRequested += () => _ = ClosePopupWithFadeAsync(NotificationsPopup, _notificationsFadeGen);
        notificationCenter.UnacknowledgedChanged += OnUnacknowledgedChanged;
        SetBellCount(notificationCenter.UnacknowledgedCount);
    }

    public void UnbindNotifications()
    {
        if (_notificationCenter is not null)
        {
            _notificationCenter.Active.CollectionChanged -= OnActiveNotificationsChanged;
            _notificationCenter.UnacknowledgedChanged -= OnUnacknowledgedChanged;
        }
        _notificationCenter = null;
        _toastHosts.Clear();
    }

    private void OnActiveNotificationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (NotificationItem item in e.NewItems)
                _toastHosts.Add(new ToastHost(item));
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
        {
            foreach (NotificationItem item in e.OldItems)
                _ = CloseToastAsync(item);
        }
    }

    private async Task CloseToastAsync(NotificationItem item)
    {
        var host = _toastHosts.FirstOrDefault(h => ReferenceEquals(h.Item, item));
        if (host is null) return;
        if (FindToastBorder(host) is { } border)
        {
            border.Classes.Add("toast-closing");
            await Task.Delay(ToastFadeOutMs);
        }
        _toastHosts.Remove(host);
    }

    private Border? FindToastBorder(ToastHost host)
    {
        foreach (var descendant in ToastItemsControl.GetVisualDescendants())
        {
            if (descendant is Border border && ReferenceEquals(border.DataContext, host))
                return border;
        }
        return null;
    }

    private void OnUnacknowledgedChanged()
    {
        if (_notificationCenter is null) return;
        SetBellCount(_notificationCenter.UnacknowledgedCount);
    }

    private void SetBellCount(int count)
    {
        var show = count > 0;
        BellBadge.IsVisible = show;
        BellBadgeText.Text = count > 99 ? "99+" : count.ToString();
    }

    public void SetTaskCenter(TaskCenterViewModel taskCenter)
    {
        Logger.Trace("Flyout: SetTaskCenter called");
        _taskCenter = taskCenter;
        TasksPopup.DataContext = taskCenter;
        taskCenter.PropertyChanged += OnTaskCenterPropertyChanged;
        UpdateTaskIndicator();
    }

    private void OnTaskCenterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_taskCenter is null) return;
        if (e.PropertyName is nameof(TaskCenterViewModel.IsOpen))
        {
            Logger.Trace($"Flyout: TaskCenter.IsOpen changed -> {_taskCenter.IsOpen}");
            if (_taskCenter.IsOpen)
            {
                Logger.Trace("Flyout: opening TasksPopup (IsOpen=true)");
                TasksPopup.IsOpen = true;
            }
            else
            {
                Logger.Trace("Flyout: TaskCenter.IsOpen=false -> fade-close TasksPopup");
                _ = ClosePopupWithFadeAsync(TasksPopup, _tasksFadeGen);
            }
        }
        else if (e.PropertyName is nameof(TaskCenterViewModel.ActiveCount))
        {
            UpdateTaskIndicator();
        }
    }

    private void OnTasksPopupOpened(object? sender, EventArgs e)
    {
        _tasksFadeGen++;
        Logger.Trace($"Flyout: TasksPopup.Opened fired (gen={_tasksFadeGen}, isOpen={TasksPopup.IsOpen}, taskCenter.IsOpen={_taskCenter?.IsOpen})");
        FadeInPopup(TasksPopup);
        Dispatcher.UIThread.Post(() =>
        {
            Logger.Trace($"Flyout: TasksPopup check (isUsingOverlay={TasksPopup.IsUsingOverlayLayer}, childBounds={TasksPopup.Child?.Bounds}, childOpacity={TasksPopup.Child?.Opacity})");
        }, DispatcherPriority.Background);
    }

    private void OnTasksPopupClosed(object? sender, EventArgs e)
    {
        Logger.Trace($"Flyout: TasksPopup.Closed fired (taskCenter.IsOpen={_taskCenter?.IsOpen})");
        if (_taskCenter is { IsOpen: true })
            _taskCenter.IsOpen = false;
    }

    private void OnNotificationsPopupOpened(object? sender, EventArgs e)
    {
        _notificationsFadeGen++;
        Logger.Trace($"Flyout: NotificationsPopup.Opened fired (gen={_notificationsFadeGen}, isOpen={NotificationsPopup.IsOpen})");
        FadeInPopup(NotificationsPopup);
        Dispatcher.UIThread.Post(() =>
        {
            Logger.Trace($"Flyout: NotificationsPopup check (isUsingOverlay={NotificationsPopup.IsUsingOverlayLayer}, childBounds={NotificationsPopup.Child?.Bounds}, childOpacity={NotificationsPopup.Child?.Opacity})");
        }, DispatcherPriority.Background);
    }

    private void OnNotificationsPopupClosed(object? sender, EventArgs e)
    {
        Logger.Trace("Flyout: NotificationsPopup.Closed fired");
    }

    private static void FadeInPopup(Popup popup)
    {
        if (popup.Child is not { } content)
        {
            Logger.Trace($"Flyout: FadeInPopup — child null for {popup.Name ?? popup.GetType().Name}");
            return;
        }
        _ = FadeInCoreAsync(popup, content);
    }

    private static async Task FadeInCoreAsync(Popup popup, Control content)
    {
        await FadeOpacityAsync(content, 0, 1, PopupFadeDuration, () => !popup.IsOpen);
        Logger.Trace($"Flyout: FadeInPopup done ({popup.Name}, opacity={content.Opacity}, isOpen={popup.IsOpen})");
    }

    private static async Task FadeOpacityAsync(Control content, double from, double to, TimeSpan duration, Func<bool>? abort = null)
    {
        const int steps = 8;
        content.Opacity = from;
        var step = TimeSpan.FromMilliseconds(duration.TotalMilliseconds / steps);
        for (var i = 1; i <= steps; i++)
        {
            await Task.Delay(step);
            if (abort?.Invoke() == true) return;
            content.Opacity = from + (to - from) * i / steps;
        }
        content.Opacity = to;
    }

    private async Task ClosePopupWithFadeAsync(Popup popup, int generation)
    {
        var isTasks = ReferenceEquals(popup, TasksPopup);
        var name = popup.Name ?? (isTasks ? "TasksPopup" : "NotificationsPopup");
        Logger.Trace($"Flyout: ClosePopupWithFadeAsync start ({name}, gen={generation}, isOpen={popup.IsOpen})");
        if (popup.Child is { } content)
            await FadeOpacityAsync(content, content.Opacity, 0, PopupFadeDuration, () => !popup.IsOpen);
        if (isTasks ? generation != _tasksFadeGen : generation != _notificationsFadeGen)
        {
            Logger.Trace($"Flyout: ClosePopupWithFadeAsync stale gen ({name}) — abort close, restore opacity");
            if (popup.Child is { } restored)
                restored.Opacity = 1;
            return;
        }
        Logger.Trace($"Flyout: ClosePopupWithFadeAsync closing ({name}, isOpen={popup.IsOpen})");
        popup.IsOpen = false;
        if (popup.Child is { } c)
            c.Opacity = 1;
    }

    private void UpdateTaskIndicator()
    {
        if (_taskCenter is null) return;
        TaskIndicatorIcon.Classes.Set("statusDot", _taskCenter.ActiveCount > 0);
        var count = _taskCenter.ActiveCount;
        TaskIndicatorBadge.IsVisible = count > 0;
        TaskIndicatorBadgeText.Text = count > 99 ? "99+" : count.ToString();
    }

    private void OnTaskIndicatorClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace($"Flyout: OnTaskIndicatorClick (notifPopupOpen={NotificationsPopup.IsOpen}, taskCenter.IsOpen={_taskCenter?.IsOpen}, taskCenterNull={_taskCenter is null})");
        if (NotificationsPopup.IsOpen)
            _ = ClosePopupWithFadeAsync(NotificationsPopup, _notificationsFadeGen);
        _taskCenter?.ToggleCommand.Execute(null);
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsPointOnElement(e, TaskIndicator) || IsPointOnElement(e, NotificationsButton))
            return;

        if (TasksPopup.IsOpen && !IsPointInPopup(e, TasksPopup))
        {
            Logger.Trace("Flyout: outside press -> fade-close tasks");
            _ = ClosePopupWithFadeAsync(TasksPopup, _tasksFadeGen);
        }

        if (NotificationsPopup.IsOpen && !IsPointInPopup(e, NotificationsPopup))
        {
            Logger.Trace("Flyout: outside press -> fade-close notifications");
            _ = ClosePopupWithFadeAsync(NotificationsPopup, _notificationsFadeGen);
        }
    }

    private static bool IsPointInPopup(PointerPressedEventArgs e, Popup popup)
    {
        if (popup.Child is not { } child) return false;
        var pos = e.GetPosition(child);
        return pos.X >= 0 && pos.Y >= 0 && pos.X <= child.Bounds.Width && pos.Y <= child.Bounds.Height;
    }

    private static bool IsPointOnElement(PointerPressedEventArgs e, Control element)
    {
        var pos = e.GetPosition(element);
        return pos.X >= 0 && pos.Y >= 0 && pos.X <= element.Bounds.Width && pos.Y <= element.Bounds.Height;
    }

    private void OnBellClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace($"Flyout: OnBellClick (notifPopupOpen={NotificationsPopup.IsOpen}, taskCenter.IsOpen={_taskCenter?.IsOpen}, notifCenterNull={_notificationCenter is null})");
        if (_notificationCenter is null) return;
        if (_taskCenter is { IsOpen: true })
            _taskCenter.IsOpen = false;
        if (NotificationsPopup.IsOpen)
            _ = ClosePopupWithFadeAsync(NotificationsPopup, _notificationsFadeGen);
        else
            NotificationsPopup.IsOpen = true;
    }

    private void OnToastPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: ToastHost host } || _notificationCenter is null) return;
        _notificationCenter.InvokeAction(host.Item);
    }

    private void OnToastActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: NotificationAction action } btn || _notificationCenter is null) return;
        try
        {
            action.Action?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MainWindow: toast action threw");
        }
        var host = btn.FindAncestorOfType<Border>()?.DataContext as ToastHost;
        if (host is not null)
            _notificationCenter.Dismiss(host.Item.Id);
    }

    private void OnToastMoreClick(object? sender, RoutedEventArgs e)
    {
        if (_notificationCenter is null) return;
        if (_taskCenter is { IsOpen: true })
            _taskCenter.IsOpen = false;
        NotificationsPopup.IsOpen = true;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            var (designWidth, designHeight) = WindowFitHelper.GetDesignSize(this);
            WindowSettingsService.SaveMainWindowSize(designWidth, designHeight);
        }

        base.OnClosing(e);
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void UpdateWindowStateIcons()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeIcon.IsVisible = !isMaximized;
        RestoreIcon.IsVisible = isMaximized;
        ToolTip.SetTip(MaximizeRestoreButton, isMaximized ? "Restore" : "Maximize");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            UpdateWindowStateIcons();
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                BeginMoveDrag(e);
        }
    }

    private void BeginResize(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(edge, e);
    }

    private void OnResizeNorthPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.North, e);
    private void OnResizeSouthPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.South, e);
    private void OnResizeEastPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.East, e);
    private void OnResizeWestPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.West, e);
    private void OnResizeNorthEastPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.NorthEast, e);
    private void OnResizeNorthWestPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.NorthWest, e);
    private void OnResizeSouthEastPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.SouthEast, e);
    private void OnResizeSouthWestPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.SouthWest, e);

    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not ListBoxItem item) return;
        if (DataContext is not MainViewModel vm) return;

        var index = item.Tag switch
        {
            "Browse" => 0,
            "Installed" => 1,
            "FileExplorer" => 2,
            "Tools" => 3,
            "Inspector" => 4,
            "Settings" => 5,
            _ => -1
        };

        if (index >= 0 && index != vm.SelectedTab)
            vm.SelectedTab = index;
    }

    private void OnBrandClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening project website from brand logo");
        Process.Start(new ProcessStartInfo("https://marcelofrau.github.io/xb-homebrew-vault/") { UseShellExecute = true });
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Escape closes the tasks / notifications flyout when open
        if (e.Key == Key.Escape)
        {
            if (NotificationsPopup.IsOpen)
            {
                Logger.Trace("Flyout: Escape -> close notifications");
                _ = ClosePopupWithFadeAsync(NotificationsPopup, _notificationsFadeGen);
                e.Handled = true;
                return;
            }

            if (_taskCenter is { IsOpen: true })
            {
                Logger.Trace("Flyout: Escape -> close tasks");
                _taskCenter.IsOpen = false;
                e.Handled = true;
                return;
            }
        }

        // Ctrl+Tab / Ctrl+Shift+Tab / Ctrl+PageDown / Ctrl+PageUp — tab switching
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrl)
        {
            var next = e.Key switch
            {
                Key.Tab => !e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                Key.PageDown => true,
                Key.PageUp => false,
                _ => (bool?)null
            };

            if (next is not null)
            {
                vm.SelectedTab = next.Value
                    ? (vm.SelectedTab + 1) % TabCount
                    : (vm.SelectedTab - 1 + TabCount) % TabCount;
                e.Handled = true;
                return;
            }
        }

        // Global scroll: PageDown/PageUp/Home/End + Down/Up arrows
        if (e.KeyModifiers == KeyModifiers.None)
        {
            if (TryHandleScroll(e.Key))
            {
                e.Handled = true;
            }
            else if (e.Key is Key.Down or Key.Up)
            {
                var focused = FocusManager?.GetFocusedElement();
                if (focused is TextBox)
                    e.Handled = true; // prevent bubble to Carousel
            }
        }
    }

    private bool TryHandleScroll(Key key)
    {
        var sv = FindNearestScrollViewer();
        if (sv is null || sv.Viewport.Height <= 0) return false;

        var offset = sv.Offset;
        var maxY = Math.Max(sv.Extent.Height - sv.Viewport.Height, 0);
        var step = sv.Viewport.Height;

        switch (key)
        {
            case Key.PageDown:
                sv.Offset = new Vector(offset.X, Math.Min(offset.Y + step, maxY));
                return true;
            case Key.PageUp:
                sv.Offset = new Vector(offset.X, Math.Max(offset.Y - step, 0));
                return true;
            case Key.Home:
                sv.Offset = new Vector(offset.X, 0);
                return true;
            case Key.End:
                sv.Offset = new Vector(offset.X, maxY);
                return true;
            case Key.Down:
                sv.Offset = new Vector(offset.X, Math.Min(offset.Y + 40, maxY));
                return true;
            case Key.Up:
                sv.Offset = new Vector(offset.X, Math.Max(offset.Y - 40, 0));
                return true;
        }

        return false;
    }

    private ScrollViewer? FindNearestScrollViewer()
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;
        if (focused is not null)
        {
            if (focused is TextBox)
                return null;

            var current = focused;
            while (current is not null)
            {
                if (current is ScrollViewer sv && sv.IsVisible && sv.Extent.Height > sv.Viewport.Height)
                {
                    if (IsChildOfTextEditor(sv))
                        return null;
                    return sv;
                }
                current = current.Parent as Visual;
            }
        }

        return FindMostRelevantScrollViewer();
    }

    private static bool IsChildOfTextEditor(Visual visual)
    {
        var current = visual.Parent as Visual;
        while (current is not null)
        {
            if (current.GetType().Name == "TextEditor")
                return true;
            current = current.Parent as Visual;
        }
        return false;
    }

    private ScrollViewer? FindMostRelevantScrollViewer()
    {
        if (ViewCarousel?.SelectedItem is not Control tab)
            return null;

        return FindFirstVisibleScrollViewer(tab);
    }

    private static ScrollViewer? FindFirstVisibleScrollViewer(Control parent)
    {
        if (parent is ScrollViewer sv && sv.IsVisible && sv.Extent.Height > sv.Viewport.Height)
            return sv;

        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control c)
            {
                var result = FindFirstVisibleScrollViewer(c);
                if (result is not null)
                    return result;
            }
        }
        return null;
    }

    private void OnErLinkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening Emulation Revival website from sidebar");
        Process.Start(new ProcessStartInfo("https://emulationrevival.github.io") { UseShellExecute = true });
    }

    private async void OnDiscordClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening Discord community popup");
        var vm = new DiscordPopupViewModel();
        var popup = new DiscordPopup { DataContext = vm };
        await popup.ShowDialog(this);
    }

    private async void OnDisconnectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("OnDisconnectClick: user clicked disconnect");
        if (DataContext is not MainViewModel vm) return;

        var confirmVm = new ConfirmViewModel(
            "Disconnect",
            "Are you sure you want to disconnect from the Xbox?",
            "Disconnect", "Cancel",
            "avares://XBVault/Assets/Views/ConfirmWindow/confirmwindow-disconnect-20.png",
            "avares://XBVault/Assets/Views/ConfirmWindow/confirmwindow-disconnect-48.png",
            isDestructive: true);
        var confirmWindow = new ConfirmWindow { DataContext = confirmVm };
        await confirmWindow.ShowDialog(this);

        if (confirmVm.Confirmed)
        {
            Logger.Info("OnDisconnectClick: confirmed, executing DisconnectCommand");
            vm.DisconnectCommand.Execute(null);
        }
        else
        {
            Logger.Trace("OnDisconnectClick: cancelled");
        }
    }
}
