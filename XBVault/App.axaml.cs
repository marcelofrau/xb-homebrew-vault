using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
            var fileExplorerViewModel = new FileExplorerViewModel(xboxService);
            var toolsViewModel = new ToolsViewModel(xboxService);
            var settingsViewModel = new SettingsViewModel(xboxService, cacheService);

            // splash first, main after delay
            var splash = new SplashWindow();
            splash.Show();

            _ = InitAfterSplashAsync(desktop, splash, mainViewModel, browseViewModel,
                installedViewModel, fileExplorerViewModel, toolsViewModel,
                settingsViewModel, xboxService, installService, erService);
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
        PackageInstallService installService,
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
                    "avares://XBVault/Assets/Views/ConfirmWindow/fluentui-collision-20.png");
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
                    "Uninstall", "Cancel",
                    "avares://XBVault/Assets/Views/InstalledView/installed-uninstall-20.png");
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

            toolsViewModel.ShowScreenshotAction = () =>
            {
                var vm = new ScreenshotViewModel(xboxService);
                vm.SaveScreenshotDialog = async stream =>
                {
                    try
                    {
                        var file = await main.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                        {
                            DefaultExtension = "png",
                            FileTypeChoices =
                            [
                                new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }
                            ]
                        });
                        if (file is null) return null;
                        await using var writeStream = await file.OpenWriteAsync();
                        await stream.CopyToAsync(writeStream);
                        return file.Name;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "SaveScreenshot failed");
                        return null;
                    }
                };
                var win = new Views.ScreenshotWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowSystemInfoAction = () =>
            {
                var vm = new SystemInfoViewModel(xboxService);
                var win = new Views.SystemInfoWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowProcessesAction = () =>
            {
                var vm = new ProcessesViewModel(xboxService);
                var win = new Views.ProcessesWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowNetworkInfoAction = () =>
            {
                var vm = new NetworkInfoViewModel(xboxService);
                var win = new Views.NetworkInfoWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowPerformanceAction = () =>
            {
                var vm = new PerformanceViewModel(xboxService);
                var win = new Views.PerformanceWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowCrashDataAction = () =>
            {
                var vm = new CrashDataViewModel(xboxService);
                var win = new Views.CrashDataWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            Action openCustomInstall = () =>
            {
                if (!xboxService.IsConnected)
                {
                    var errDlg = new ErrorDialog(
                        "Not Connected",
                        "Connect to an Xbox first before using Custom Install.",
                        "Go to the sidebar and connect to your Xbox Developer Mode console.",
                        ErrorDialogType.Warn);
                    errDlg.ShowDialog(main);
                    return;
                }
                var vm = new CustomInstallViewModel(xboxService, installService);
                vm.PickFileAsync = async () =>
                {
                    try
                    {
                        var files = await main.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                        {
                            Title = "Select Package",
                            AllowMultiple = false,
                            FileTypeFilter =
                            [
                                new FilePickerFileType("Package files")
                                {
                                    Patterns = ["*.appx", "*.msix", "*.appxbundle", "*.msixbundle", "*.zip"]
                                }
                            ]
                        });
                        return files?.FirstOrDefault()?.TryGetLocalPath();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "CustomInstall file picker failed");
                        return null;
                    }
                };
                var win = new Views.CustomInstallWindow { DataContext = vm };
                vm.CloseAction = () => win.Close();
                win.ShowDialog(main);
            };
            toolsViewModel.ShowCustomInstallAction = openCustomInstall;
            browseViewModel.ShowCustomInstallAction = openCustomInstall;

            toolsViewModel.ShowConfirmAsync = async (title, message, confirmText, cancelText, iconSource) =>
            {
                var vm = new ConfirmViewModel(title, message, confirmText, cancelText, iconSource);
                var win = new Views.ConfirmWindow { DataContext = vm };
                await win.ShowDialog(main);
                return vm.Confirmed;
            };

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
