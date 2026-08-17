using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileMainWindow : UserControl
{
    private static readonly IBrush ActiveTabBrush = new SolidColorBrush(Color.Parse("#9ACA3C"));
    private static readonly IBrush InactiveTabBrush = new SolidColorBrush(Color.Parse("#888888"));

    public MobileMainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateIndicators(0);
        ApplySafeAreaPadding();
    }

    private void ApplySafeAreaPadding()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InsetsManager == null) return;

        var safe = topLevel.InsetsManager.SafeAreaPadding;
        XBVault.Services.Logger.Info($"SafeAreaPadding: Top={safe.Top} Bottom={safe.Bottom} Left={safe.Left} Right={safe.Right}");

        if (safe.Top > 0 || safe.Bottom > 0)
        {
            TopBarContent.Padding = new Thickness(0, safe.Top, 0, 0);
            BottomBarContent.Padding = new Thickness(0, 0, 0, safe.Bottom);
        }
        else
        {
            TopBarContent.Padding = new Thickness(0, 25, 0, 0);
            BottomBarContent.Padding = new Thickness(0, 0, 0, 48);
        }
    }

    public void SetDataContext(MainViewModel vm)
    {
        DataContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsXboxConnected))
                UpdateConnectionIcon();
        };
        UpdateConnectionIcon();
    }

    private void UpdateConnectionIcon()
    {
        if (DataContext is not MainViewModel vm) return;
        var uri = vm.IsXboxConnected
            ? new Uri("avares://XBVault/Assets/Views/MainWindow/mainwindow-status-connected-16.png")
            : vm.IsNotConfigured
                ? new Uri("avares://XBVault/Assets/Views/MainWindow/mainwindow-status-notconfigured-16.png")
                : new Uri("avares://XBVault/Assets/Views/MainWindow/mainwindow-status-disconnected-16.png");
        try
        {
            ConnectionIcon.Source = new Bitmap(AssetLoader.Open(uri));
        }
        catch (Exception ex)
        {
            XBVault.Services.Logger.Debug($"Failed to load connection icon: {ex.Message}");
        }
    }

    private void OnTabBrowse(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTab = 0;
        UpdateIndicators(0);
    }

    private void OnTabInstalled(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTab = 1;
        UpdateIndicators(1);
    }

    private void OnTabFiles(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTab = 2;
        UpdateIndicators(2);
    }

    private void OnTabTools(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTab = 3;
        UpdateIndicators(3);
    }

    private void UpdateIndicators(int selected)
    {
        TabBrowseLabel.Foreground = selected == 0 ? ActiveTabBrush : InactiveTabBrush;
        TabInstalledLabel.Foreground = selected == 1 ? ActiveTabBrush : InactiveTabBrush;
        TabFilesLabel.Foreground = selected == 2 ? ActiveTabBrush : InactiveTabBrush;
        TabToolsLabel.Foreground = selected == 3 ? ActiveTabBrush : InactiveTabBrush;
    }

    private void OnConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ConnectCommand.Execute(null);
    }

    private void OnHamburgerClick(object? sender, RoutedEventArgs e)
    {
        // Flyout is handled by Avalonia
    }

    private void OnMenuNotifications(object? sender, RoutedEventArgs e)
    {
        // TODO: Fase 2 - Notifications page
    }

    private void OnMenuJobs(object? sender, RoutedEventArgs e)
    {
        // TODO: Fase 2 - Jobs page
    }

    private void OnMenuLogs(object? sender, RoutedEventArgs e)
    {
        // TODO: Fase 2 - Logs page
    }

    private void OnMenuSettings(object? sender, RoutedEventArgs e)
    {
        // TODO: Fase 2 - Settings page
    }

    private void OnMenuAbout(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenAboutCommand.Execute(null);
    }
}
