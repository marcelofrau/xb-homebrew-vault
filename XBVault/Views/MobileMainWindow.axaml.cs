using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileMainWindow : UserControl
{
    private static IBrush? _activeTabBrush;
    private static IBrush? _inactiveTabBrush;
    private static readonly IBrush ActiveTabBgBrush = new SolidColorBrush(Color.Parse("#229ACA3C"));

    private static IBrush ActiveTabBrush => _activeTabBrush ??= (IBrush)Application.Current!.FindResource("AccentBrush")!;
    private static IBrush InactiveTabBrush => _inactiveTabBrush ??= (IBrush)Application.Current!.FindResource("TextMutedBrush")!;

    public ViewModels.SettingsViewModel? SettingsViewModel { get; set; }
    public NotificationCenterService? NotificationCenter { get; set; }
    public BackgroundTaskService? BackgroundTasks { get; set; }
    public IXboxAuthService? AuthService { get; set; }
    private Action? _currentOverlayBackAction;

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
                XBVault.Services.Logger.Info("Android: Back button → run overlay back action + close");
                _currentOverlayBackAction?.Invoke();
                _currentOverlayBackAction = null;
                NavigationPanel.Children.Clear();
                NavigationPanel.IsVisible = false;
                return true;
            }
            return false;
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

    public void ShowOverlay(UserControl view, Action? onBack = null)
    {
        _currentOverlayBackAction = onBack;
        NavigationPanel.Children.Clear();
        NavigationPanel.Children.Add(view);
        NavigationPanel.IsVisible = true;
    }

    public void CloseOverlay()
    {
        _currentOverlayBackAction?.Invoke();
        _currentOverlayBackAction = null;
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
            ? new Uri("avares://XBVault/Assets/Views/MainWindow/mainwindow-disconnect-32.png")
            : new Uri("avares://XBVault/Assets/Views/MainWindow/mainwindow-connect-32.png");
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
        {
            if (!SettingsService.Current.XboxConnection.IsConfigured)
            {
                XBVault.Services.Logger.Info("Android: Not configured, opening setup wizard");
                ShowSetupWizard();
            }
            else
            {
                vm.ConnectCommand.Execute(null);
            }
        }
    }

    private void ShowSetupWizard()
    {
        if (AuthService == null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var wizardVm = new SetupWizardViewModel(AuthService);
            var wizardView = new MobileSetupWizardView();
            wizardView.SetViewModel(wizardVm);
            wizardVm.CloseAction = () => CloseOverlay();
            wizardView.CloseRequested += (_, _) => CloseOverlay();
            ShowOverlay(wizardView);
        });
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
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var view = new MobileNotificationsView();
            if (NotificationCenter != null)
                view.DataContext = NotificationCenter;
            view.SetOnBack(() => CloseOverlay());
            ShowOverlay(view);
        });
    }

    private void OnMenuJobs(object? sender, RoutedEventArgs e)
    {
        CloseHamburgerFlyout();
        XBVault.Services.Logger.Info("Android: Menu → Jobs clicked");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var view = new MobileJobsView();
            if (BackgroundTasks != null)
                view.DataContext = BackgroundTasks;
            view.SetOnBack(() => CloseOverlay());
            ShowOverlay(view);
        });
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
            settings.SetOnBack(() => CloseOverlay());
            ShowOverlay(settings);
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
            about.SetOnBack(() => CloseOverlay());
            ShowOverlay(about);
            XBVault.Services.Logger.Info("Android: About overlay opened");
        });
    }
}
