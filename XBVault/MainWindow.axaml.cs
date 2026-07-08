using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using XBVault.Helpers;
using XBVault.Services;
using XBVault.ViewModels;
using XBVault.Views;

namespace XBVault;

public partial class MainWindow : Window
{
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
    }

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
            WindowSettingsService.SaveMainWindowSize(Width, Height);

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
