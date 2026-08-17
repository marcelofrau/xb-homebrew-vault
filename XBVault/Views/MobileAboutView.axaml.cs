using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Helpers;

namespace XBVault.Views;

public partial class MobileAboutView : UserControl
{
    private static readonly string RevivesUrl = "https://discord.gg/cBYsQCS7j7";
    private static readonly string XboxHubUrl = "https://discord.gg/pVd47KAG24";
    private static readonly string ErUrl = "https://discord.gg/j2HndpJTej";

    private Action? _onBack;

    public MobileAboutView()
    {
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();

    private void OnChangelogClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/marcelofrau/xb-homebrew-vault/releases") { UseShellExecute = true });
    }

    private void OnDiscordClick(object? sender, RoutedEventArgs e)
    {
        // Flyout opens automatically from AXAML
    }

    private void OnDiscordRevives(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(RevivesUrl) { UseShellExecute = true });
    }

    private void OnDiscordXboxHub(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(XboxHubUrl) { UseShellExecute = true });
    }

    private void OnDiscordEr(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ErUrl) { UseShellExecute = true });
    }

    private void OnErLinkClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://emulationrevival.github.io") { UseShellExecute = true });
    }

    private void OnProjectLinkClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/marcelofrau/xb-homebrew-vault") { UseShellExecute = true });
    }
}

