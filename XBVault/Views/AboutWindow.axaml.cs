#nullable enable
using System.Diagnostics;
using Avalonia.Controls;
using XBVault.Helpers;
using XBVault.Services;

namespace XBVault.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
        Opened += (_, _) => Logger.Debug("AboutWindow opened");
        Opened += (_, _) => WindowFitHelper.ApplyScale(this, SettingsService.Current.UiScale);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Trace("AboutWindow closed");
        Close();
    }

    private void OnErLinkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening Emulation Revival website from About window");
        Process.Start(new ProcessStartInfo(AppUrls.EmulationRevival) { UseShellExecute = true });
    }

    private void OnProjectLinkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening project GitHub from About window");
        Process.Start(new ProcessStartInfo(AppUrls.GitHubRepo) { UseShellExecute = true });
    }

    private void OnChangelogClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Info("Opening releases page from About window");
        Process.Start(new ProcessStartInfo(AppUrls.GitHubReleases) { UseShellExecute = true });
    }
}
