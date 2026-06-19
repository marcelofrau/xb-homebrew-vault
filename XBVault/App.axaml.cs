using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using XBVault.Services;
using XBVault.ViewModels;
using XBVault.Views;

namespace XBVault;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SetupGlobalExceptionHandling();

        // Apply saved log level from settings
        var savedLevel = SettingsService.Current.MinLogLevel;
        Logger.MinLevel = savedLevel switch
        {
            "Trace" => LogLevel.Trace,
            "Debug" => LogLevel.Debug,
            "Info"  => LogLevel.Info,
            "Warn"  => LogLevel.Warn,
            "Error" => LogLevel.Error,
            "Fatal" => LogLevel.Fatal,
            _       => LogLevel.Info
        };
        Logger.Debug($"Log level initialized to {savedLevel}");

        Logger.Info("Application initialized");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var xboxService = new XboxDeviceService();
            var erService = new EmulationRevivalService();
            var cacheService = new CacheService();
            var installService = new PackageInstallService(cacheService, xboxService);

            var mainViewModel = new MainViewModel(xboxService);
            var browseViewModel = new BrowseViewModel(erService, installService, xboxService);
            var installedViewModel = new InstalledViewModel(xboxService);
            var fileExplorerViewModel = new FileExplorerViewModel();
            var toolsViewModel = new ToolsViewModel();
            var settingsViewModel = new SettingsViewModel(xboxService, cacheService);

            // splash first, main after delay
            var splash = new SplashWindow();
            splash.Show();

            _ = InitAfterSplashAsync(desktop, splash, mainViewModel, browseViewModel,
                installedViewModel, fileExplorerViewModel, toolsViewModel,
                settingsViewModel, xboxService, erService);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetupGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Fatal(ex ?? new Exception("Unknown"), "AppDomain unhandled exception");
            ShowErrorDialogSafe("Fatal Error", "An unrecoverable error occurred.", ex?.ToString() ?? "Unknown error", ErrorDialogType.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        if (Dispatcher.UIThread is { } dispatcher)
        {
            dispatcher.UnhandledException += (_, e) =>
            {
                Logger.Error(e.Exception, "Dispatcher unhandled exception");
                ShowErrorDialogSafe("Error", "An unexpected error occurred in the UI.", e.Exception.ToString(), ErrorDialogType.Error);
                e.Handled = true;
            };
        }
    }

    private static void ShowErrorDialogSafe(string title, string description, string details, ErrorDialogType type)
    {
        try
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                    var dlg = new ErrorDialog(title, description, details, type);
                    if (owner is not null)
                        await dlg.ShowDialog(owner);
                    else
                        dlg.Show();
                }
                catch { }
            }, DispatcherPriority.Send);
        }
        catch { }
    }

    private static async Task InitAfterSplashAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        SplashWindow splash,
        MainViewModel mainViewModel,
        BrowseViewModel browseViewModel,
        InstalledViewModel installedViewModel,
        FileExplorerViewModel fileExplorerViewModel,
        ToolsViewModel toolsViewModel,
        SettingsViewModel settingsViewModel,
        XboxDeviceService xboxService,
        EmulationRevivalService erService)
    {
        Logger.Debug("Splash delay starting (2s)");
        await Task.Delay(2000);
        Logger.Debug("Splash delay complete, building main window");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var main = new MainWindow
            {
                DataContext = mainViewModel
            };

            desktop.MainWindow = main;
            main.Show();

            browseViewModel.ShowDetailAction = item =>
            {
                Logger.Info($"ShowDetailAction invoked for: {item.Name}");
                try
                {
                    var detail = new Views.ItemDetailWindow { DataContext = browseViewModel };
                    Logger.Info("ItemDetailWindow created");
                    detail.Closed += (_, _) =>
                    {
                        Logger.Info("ItemDetailWindow closed — resetting SelectedItem");
                        browseViewModel.SelectedItem = null;
                    };
                    browseViewModel.CloseDetailAction = () => detail.Close();
                    Logger.Info("Calling ShowDialog on ItemDetailWindow");
                    detail.ShowDialog(main);
                    Logger.Info("ShowDialog returned");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Exception opening ItemDetailWindow for {item.Name}");
                }
            };

            mainViewModel.ShowAboutAction = () =>
            {
                var about = new Views.AboutWindow();
                about.ShowDialog(main);
            };

            mainViewModel.ShowConnectAction = async () =>
            {
                var connVm = new ConnectionViewModel(xboxService);
                var connWindow = new Views.ConnectionWindow
                {
                    DataContext = connVm
                };
                await connWindow.ShowDialog(main);

                if (!connVm.IsSuccess)
                {
                    var errDlg = new ErrorDialog(
                        "Connection Failed",
                        "Could not establish a connection to the Xbox. Verify the address and credentials in Settings.",
                        "Check your Xbox Developer Mode settings:\n" +
                        "- Ensure Xbox is in Developer Mode\n" +
                        "- Verify the IP address is correct\n" +
                        "- Confirm the username and password are correct\n" +
                        "- Make sure the Xbox is powered on and didn't go to sleep\n" +
                        "- Make sure both devices are on the same network",
                        ErrorDialogType.Warn);
                    await errDlg.ShowDialog(main);
                }

                return connVm.IsSuccess;
            };

            browseViewModel.ShowRefreshDialogAsync = async () =>
            {
                var refreshVm = new RefreshViewModel(erService, async () =>
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await browseViewModel.LoadCatalogCommand.ExecuteAsync(null);
                    });
                });
                var refreshWindow = new Views.RefreshWindow { DataContext = refreshVm };
                await refreshWindow.ShowDialog(main);
            };

            var exitConfirmed = false;
            main.Closing += async (_, e) =>
            {
                if (exitConfirmed) return;
                e.Cancel = true;
                var confirmVm = new ConfirmViewModel(
                    "Exit",
                    "Are you sure you want to exit?",
                    "Exit", "Cancel",
                    isExit: true);
                var confirmWindow = new Views.ConfirmWindow { DataContext = confirmVm };
                await confirmWindow.ShowDialog(main);
                if (confirmVm.Confirmed)
                {
                    exitConfirmed = true;
                    main.Close();
                }
            };

            var browseView = new Views.BrowseView { DataContext = browseViewModel };

            installedViewModel.ConfirmUninstallAsync = async pkg =>
            {
                var confirmVm = new ConfirmViewModel(
                    "Uninstall Package",
                    $"Are you sure you want to uninstall {pkg.Name}?",
                    "Uninstall", "Cancel");
                var confirmWindow = new Views.ConfirmWindow { DataContext = confirmVm };
                await confirmWindow.ShowDialog(main);
                return confirmVm.Confirmed;
            };

            var installedView = new Views.InstalledView { DataContext = installedViewModel };
            settingsViewModel.ShowConnectDialogAsync = async () =>
            {
                var connVm = new ConnectionViewModel(xboxService);
                var connWindow = new Views.ConnectionWindow { DataContext = connVm };
                await connWindow.ShowDialog(main);
                return connVm.IsSuccess;
            };

            var fileExplorerView = new Views.FileExplorerView { DataContext = fileExplorerViewModel };
            var toolsView = new Views.ToolsView { DataContext = toolsViewModel };
            var settingsView = new Views.SettingsView { DataContext = settingsViewModel };
            var logsView = new Views.LogsView { DataContext = new LogsViewModel() };

            main.ViewCarousel.Items.Add(browseView);
            main.ViewCarousel.Items.Add(installedView);
            main.ViewCarousel.Items.Add(fileExplorerView);
            main.ViewCarousel.Items.Add(toolsView);
            main.ViewCarousel.Items.Add(settingsView);
            main.ViewCarousel.Items.Add(logsView);

            // kick off background loads
            _ = browseViewModel.LoadCatalogCommand.ExecuteAsync(null);
            // Installed packages loaded only on explicit refresh (manual connect)

            Logger.Info("Main window loaded, splash closed");

            splash.Close();
        });
    }
}
