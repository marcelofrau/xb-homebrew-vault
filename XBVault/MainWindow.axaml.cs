using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using XBVault.Helpers;
using XBVault.Services;
using XBVault.ViewModels;
using XBVault.Views;

namespace XBVault;

public partial class MainWindow : Window
{
    private const int TabCount = 7;

    public MainWindow()
    {
        InitializeComponent();
        MinWidth = WindowSettingsService.MinMainWindowWidth;
        MinHeight = WindowSettingsService.MinMainWindowHeight;
        var size = WindowSettingsService.GetMainWindowSize();
        Width = size.Width;
        Height = size.Height;
        VersionText.Text = BuildInfo.DisplayVersion;
        UpdateWindowStateIcons();
        AddHandler(InputElement.KeyDownEvent, OnMainWindowKeyDown, RoutingStrategies.Tunnel);
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

    private void OnBrandClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening project website from brand logo");
        Process.Start(new ProcessStartInfo("https://marcelofrau.github.io/xb-homebrew-vault/") { UseShellExecute = true });
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

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
