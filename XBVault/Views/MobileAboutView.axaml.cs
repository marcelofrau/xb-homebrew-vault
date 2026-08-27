using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Helpers;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileAboutView : UserControl
{
    private static readonly string RevivesUrl = AppUrls.DiscordRevives;
    private static readonly string XboxHubUrl = AppUrls.DiscordXboxHub;
    private static readonly string ErUrl = AppUrls.DiscordEmuRevival;

    private Action? _onBack;

    public MobileAboutView()
    {
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
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
        PlatformHelper.OpenUrl(AppUrls.GitHubReleases);
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
        PlatformHelper.OpenUrl(AppUrls.EmulationRevival);
    }

    private void OnProjectLinkClick(object? sender, RoutedEventArgs e)
    {
        PlatformHelper.OpenUrl(AppUrls.GitHubRepo);
    }
}

