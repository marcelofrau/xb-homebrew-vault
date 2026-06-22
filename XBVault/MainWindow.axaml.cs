using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using XBVault.Helpers;
using XBVault.Services;

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

    private void OnErLinkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening Emulation Revival website from sidebar");
        Process.Start(new ProcessStartInfo("https://emulationrevival.github.io") { UseShellExecute = true });
    }
}
