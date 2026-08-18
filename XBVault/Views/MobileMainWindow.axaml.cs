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
    private static readonly IBrush ActiveTabBgBrush = new SolidColorBrush(Color.Parse("#229ACA3C"));

    public ViewModels.SettingsViewModel? SettingsViewModel { get; set; }

    public MobileMainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateIndicators(0);
        ApplySafeAreaPadding();

        // Android back button: intercept at Activity level
        Services.AndroidBackHandler.OnBack = () =>
        {
            if (NavigationPanel.IsVisible)
            {
                XBVault.Services.Logger.Info("Android: Back button → close overlay");
                NavigationPanel.Children.Clear();
                NavigationPanel.IsVisible = false;
                return true; // handled, don't close Activity
            }
            return false; // not handled, minimize app
        };
    }

    private void ApplySafeAreaPadding()
    {
        // Commented out — device handles insets automatically
        // var topLevel = TopLevel.GetTopLevel(this);
        // if (topLevel?.InsetsManager == null) return;
        // var safe = topLevel.InsetsManager.SafeAreaPadding;
        // if (safe.Top > 0 || safe.Bottom > 0)
        // {
        //     TopBarContent.Padding = new Thickness(0, safe.Top, 0, 0);
        //     BottomBarContent.Padding = new Thickness(0, 0, 0, safe.Bottom);
        // }
        // else
        // {
        //     TopBarContent.Padding = new Thickness(0, 25, 0, 0);
        //     BottomBarContent.Padding = new Thickness(0, 0, 0, 48);
        // }
    }

    public void ShowOverlay(UserControl view)
    {
        NavigationPanel.Children.Clear();
        NavigationPanel.Children.Add(view);
        NavigationPanel.IsVisible = true;
    }

    public void CloseOverlay()
    {
        NavigationPanel.Children.Clear();
        NavigationPanel.IsVisible = false;
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
        XBVault.Services.Logger.Info("Android: Tab switch → Browse");
        if (DataContext is MainViewModel vm) vm.SelectedTab = 0;
        UpdateIndicators(0);
    }

    private void OnTabInstalled(object? sender, RoutedEventArgs e)
    {
        XBVault.Services.Logger.Info("Android: Tab switch → Catalog");
        if (DataContext is MainViewModel vm) vm.SelectedTab = 1;
        UpdateIndicators(1);
    }

    private void OnTabFiles(object? sender, RoutedEventArgs e)
    {
        XBVault.Services.Logger.Info("Android: Tab switch → Explorer");
        if (DataContext is MainViewModel vm) vm.SelectedTab = 2;
        UpdateIndicators(2);
    }

    private void OnTabTools(object? sender, RoutedEventArgs e)
    {
        XBVault.Services.Logger.Info("Android: Tab switch → Tools");
        if (DataContext is MainViewModel vm) vm.SelectedTab = 3;
        UpdateIndicators(3);
    }

    private void UpdateIndicators(int selected)
    {
        TabBrowseLabel.Foreground = selected == 0 ? ActiveTabBrush : InactiveTabBrush;
        TabInstalledLabel.Foreground = selected == 1 ? ActiveTabBrush : InactiveTabBrush;
        TabFilesLabel.Foreground = selected == 2 ? ActiveTabBrush : InactiveTabBrush;
        TabToolsLabel.Foreground = selected == 3 ? ActiveTabBrush : InactiveTabBrush;

        TabBrowseBtn.Background = selected == 0 ? ActiveTabBgBrush : Avalonia.Media.Brushes.Transparent;
        TabInstalledBtn.Background = selected == 1 ? ActiveTabBgBrush : Avalonia.Media.Brushes.Transparent;
        TabFilesBtn.Background = selected == 2 ? ActiveTabBgBrush : Avalonia.Media.Brushes.Transparent;
        TabToolsBtn.Background = selected == 3 ? ActiveTabBgBrush : Avalonia.Media.Brushes.Transparent;
    }

    private void OnConnectionClick(object? sender, RoutedEventArgs e)
    {
        XBVault.Services.Logger.Info("Android: Connection icon clicked");
        if (DataContext is MainViewModel vm)
            vm.ConnectCommand.Execute(null);
    }

    private void OnHamburgerClick(object? sender, RoutedEventArgs e)
    {
        XBVault.Services.Logger.Info("Android: Hamburger menu opened");
    }

    private void CloseHamburgerFlyout()
    {
        HamburgerBtn.Flyout?.IsOpen = false;
    }

    private void OnMenuNotifications(object? sender, RoutedEventArgs e)
    {
        CloseHamburgerFlyout();
        XBVault.Services.Logger.Info("Android: Menu → Notifications clicked");
        // TODO: Fase 2 - Notifications page
    }

    private void OnMenuJobs(object? sender, RoutedEventArgs e)
    {
        CloseHamburgerFlyout();
        XBVault.Services.Logger.Info("Android: Menu → Jobs clicked");
        // TODO: Fase 2 - Jobs page
    }

    private void OnMenuLogs(object? sender, RoutedEventArgs e)
    {
        CloseHamburgerFlyout();
        XBVault.Services.Logger.Info("Android: Menu → Logs clicked");
        // TODO: Fase 2 - Logs page
    }

    private void OnMenuSettings(object? sender, RoutedEventArgs e)
    {
        CloseHamburgerFlyout();
        XBVault.Services.Logger.Info("Android: Menu → Settings clicked");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var settings = new MobileSettingsView();
            if (SettingsViewModel != null)
                settings.DataContext = SettingsViewModel;
            settings.SetOnBack(() =>
            {
                XBVault.Services.Logger.Info("Android: Settings → back");
                NavigationPanel.Children.Clear();
                NavigationPanel.IsVisible = false;
            });
            NavigationPanel.Children.Clear();
            NavigationPanel.Children.Add(settings);
            NavigationPanel.IsVisible = true;
            XBVault.Services.Logger.Info("Android: Settings overlay opened");
        });
    }

    private void OnMenuAbout(object? sender, RoutedEventArgs e)
    {
        CloseHamburgerFlyout();
        XBVault.Services.Logger.Info("Android: Menu → About clicked");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var about = new MobileAboutView();
            about.SetOnBack(() =>
            {
                XBVault.Services.Logger.Info("Android: About → back");
                NavigationPanel.Children.Clear();
                NavigationPanel.IsVisible = false;
            });
            NavigationPanel.Children.Clear();
            NavigationPanel.Children.Add(about);
            NavigationPanel.IsVisible = true;
            XBVault.Services.Logger.Info("Android: About overlay opened");
        });
    }
}
