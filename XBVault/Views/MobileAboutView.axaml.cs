using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Helpers;
using XBVault.Services;

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
        AttachedToVisualTree += OnAttached;
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Commented out — device handles insets automatically
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();

    private void OnChangelogClick(object? sender, RoutedEventArgs e)
    {
        PlatformHelper.OpenUrl("https://github.com/marcelofrau/xb-homebrew-vault/releases");
    }

    private void OnDiscordClick(object? sender, RoutedEventArgs e)
    {
        // Flyout opens automatically from AXAML
    }

    private void OnDiscordRevives(object? sender, RoutedEventArgs e)
    {
        PlatformHelper.OpenUrl(RevivesUrl);
    }

    private void OnDiscordXboxHub(object? sender, RoutedEventArgs e)
    {
        PlatformHelper.OpenUrl(XboxHubUrl);
    }

    private void OnDiscordEr(object? sender, RoutedEventArgs e)
    {
        PlatformHelper.OpenUrl(ErUrl);
    }

    private void OnErLinkClick(object? sender, RoutedEventArgs e)
    {
        PlatformHelper.OpenUrl("https://emulationrevival.github.io");
    }

    private void OnProjectLinkClick(object? sender, RoutedEventArgs e)
    {
        PlatformHelper.OpenUrl("https://github.com/marcelofrau/xb-homebrew-vault");
    }
}

