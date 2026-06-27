using System.Diagnostics;
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
        VersionText.Text = BuildInfo.DisplayVersion;
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

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
