using System;
using System.Collections.Generic;
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
    private readonly List<int> _tabHistory = new();
    private int _currentTab;

    public MobileMainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateIndicators(0);
        ApplySafeAreaPadding();

        // Wire back navigation: BackRequested (Avalonia native, works on all Android)
        // + AndroidBackHandler.OnBack as fallback for older API levels
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
            topLevel.BackRequested += OnBackRequested;

        Services.AndroidBackHandler.OnBack = () => HandleBackRequest();
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (HandleBackRequest())
            e.Handled = true;
    }

    private bool HandleBackRequest()
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
        if (_tabHistory.Count > 0)
        {
            var prevTab = _tabHistory[^1];
            _tabHistory.RemoveAt(_tabHistory.Count - 1);
            XBVault.Services.Logger.Info($"Android: Back button → navigate to tab {prevTab} (history depth={_tabHistory.Count})");
            SwitchToTab(prevTab, pushHistory: false);
            return true;
        }
        if (_currentTab != 0)
        {
            XBVault.Services.Logger.Info("Android: Back button → navigate to Browse (home)");
            SwitchToTab(0, pushHistory: false);
            return true;
        }
        // On Browse with empty history → allow Activity to finish (app exit)
        return false;
    }

    private void ApplySafeAreaPadding()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InsetsManager == null) return;

        // Disable Avalonia's automatic safe area padding — we handle it manually
        // to avoid double-padding (Avalonia auto + our margins)
        TopLevel.SetAutoSafeAreaPadding(this, false);

        var insets = topLevel.InsetsManager;

        // Ensure system bars use dark theme (white icons on dark background)
        try
        {
            insets.SystemBarColor = Color.Parse("#0D1117");
        }
        catch (Exception ex)
        {
            Services.Logger.Debug($"Android: Could not set SystemBarColor: {ex.Message}");
        }

        // Safe area padding — apply real values or reset to zero (old Android)
        var safe = insets.SafeAreaPadding;
        Services.Logger.Info($"Android: SafeAreaPadding top={safe.Top} bottom={safe.Bottom} left={safe.Left} right={safe.Right}");

        ApplyInsets(safe.Top, safe.Bottom);

        // Subscribe for dynamic changes (rotation, system bar visibility)
        insets.SafeAreaChanged += (_, args) =>
        {
            var s = args.SafeAreaPadding;
            Services.Logger.Info($"Android: SafeAreaChanged top={s.Top} bottom={s.Bottom}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyInsets(s.Top, s.Bottom));
        };
    }

    private void ApplyInsets(double top, double bottom)
    {
        if (top > 0 || bottom > 0)
        {
            // Android 15+ (edge-to-edge): apply real insets
            RootGrid.Margin = new Thickness(0, top, 0, 0);
            BottomBarContent.Margin = new Thickness(0, 0, 0, bottom);
            NavigationPanel.Margin = new Thickness(0, 0, 0, bottom);
            Services.Logger.Info($"Android: Applied safe area padding top={top} bottom={bottom}");
        }
        else
        {
            // Old Android: no edge-to-edge, no padding needed (matches v2.0.0 behavior)
            RootGrid.Margin = new Thickness(0);
            BottomBarContent.Margin = new Thickness(0);
            NavigationPanel.Margin = new Thickness(0);
            Services.Logger.Info("Android: SafeAreaPadding zero, no margins applied");
        }
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
        SwitchToTab(0);
    }

    private void OnTabInstalled(object? sender, RoutedEventArgs e)
    {
        SwitchToTab(1);
    }

    private void OnTabFiles(object? sender, RoutedEventArgs e)
    {
        SwitchToTab(2);
    }

    private void OnTabTools(object? sender, RoutedEventArgs e)
    {
        SwitchToTab(3);
    }

    private void SwitchToTab(int index, bool pushHistory = true)
    {
        if (pushHistory && _currentTab != index)
            _tabHistory.Add(_currentTab);

        _currentTab = index;
        if (DataContext is MainViewModel vm) vm.SelectedTab = index;
        UpdateIndicators(index);
        XBVault.Services.Logger.Info($"Android: Tab switch → {index} (history depth={_tabHistory.Count})");
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
            if (vm.IsXboxConnected)
            {
                XBVault.Services.Logger.Info("Android: Connected, disconnecting");
                vm.DisconnectCommand.Execute(null);
            }
            else if (!SettingsService.Current.WizardCompleted && !SettingsService.Current.XboxConnection.IsConfigured)
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
            wizardView.CloseRequested += async (_, _) =>
            {
                CloseOverlay();
                if (wizardVm.WasCompleted && DataContext is MainViewModel vm)
                {
                    XBVault.Services.Logger.Info("Android: Wizard completed from connect — auto-connecting");
                    if (vm.ShowConnectAction is not null)
                        _ = vm.ShowConnectAction();
                }
            };
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
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var vm = new ViewModels.LogsViewModel();
            var view = new MobileLogsView { DataContext = vm };
            view.SetOnBack(() => CloseOverlay());
            ShowOverlay(view);
        });
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
