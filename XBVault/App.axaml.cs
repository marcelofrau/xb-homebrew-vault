#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Diagnostics;
using System.IO;
using XBVault.Helpers;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;
using XBVault.Views;

namespace XBVault;

public partial class App : Application
{
    private const int SplashMinDelayMs = 2000;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SetupGlobalExceptionHandling();

        Logger.Init();

        // Log pre-flight repairs from Program.Main
        if (AppBoot.PreFlightReport is { } report)
        {
            if (report.SettingsReset)
                Logger.Warn($"Pre-flight: settings were corrupted, reset to defaults");
            if (report.CacheCleared)
                Logger.Warn("Pre-flight: cache was corrupted or incompatible, cleared");
            if (report.LogDirUnavailable)
                Logger.Warn("Pre-flight: log directory unavailable, file logging disabled");
            foreach (var w in report.Warnings)
                Logger.Warn($"Pre-flight: {w}");
            foreach (var e in report.Errors)
                Logger.Error($"Pre-flight error: {e}");
        }

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

        if (ApplicationLifetime is IActivityApplicationLifetime)
            Logger.MinLevel = LogLevel.Trace;

        LogGpuInfo();

        Logger.Info("Application initialized");

        if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            // Android: root panel holds splash initially, swaps to main after init
            Logger.Info($"Android: IActivityApplicationLifetime detected @ {DateTime.Now:HH:mm:ss.fff}");
            var rootPanel = new Panel();
            var splash = new Views.MobileSplashView();
            rootPanel.Children.Add(splash);
            Logger.Info($"Android: splash added to rootPanel @ {DateTime.Now:HH:mm:ss.fff}");
            activity.MainViewFactory = () =>
            {
                Logger.Info($"Android: MainViewFactory called @ {DateTime.Now:HH:mm:ss.fff}");
                return rootPanel;
            };
            Logger.Info($"Android: MainViewFactory set, launching init @ {DateTime.Now:HH:mm:ss.fff}");

            _ = InitAndroidAfterSplashAsync(rootPanel, splash);
        }
        else if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var authService = new XboxAuthService();
            var packageService = new XboxPackageService(authService);
            var systemService = new XboxSystemService(authService);
            var networkService = new XboxNetworkService(authService);
            var processService = new XboxProcessService(authService);
            var performanceService = new XboxPerformanceService(authService);
            var cacheService = new CacheService();
            var installService = new PackageInstallService(cacheService, packageService);
            var sftpService = new SftpService();
            var sftpTransferService = new SftpTransferService(sftpService);
            var portalService = new PortalAppFilesService(authService, packageService);
            var catalogService = new CatalogApiService();
            var overrideService = new PackageOverrideService();
            overrideService.Initialize();
            var versionChecker = new VersionCheckerService(overrideService);
            var backgroundTaskService = new BackgroundTaskService();
            backgroundTaskService.Start();
            var notificationCenter = new NotificationCenterService();
            var taskCenterViewModel = new TaskCenterViewModel(backgroundTaskService);

            var mainViewModel = new MainViewModel(authService);
            var browseViewModel = new BrowseViewModel(installService, authService, packageService, catalogService, overrideService, versionChecker);
            var installedViewModel = new InstalledViewModel(authService, packageService);
            var fileExplorerViewModel = new FileExplorerViewModel(authService, sftpService, sftpTransferService, portalService);
            var toolsViewModel = new ToolsViewModel(authService, systemService);
            var settingsViewModel = new SettingsViewModel(authService, cacheService);

            var updateService = new InstalledAppUpdateService(authService, packageService, versionChecker, notificationCenter, backgroundTaskService);

            // splash first, main after delay
            var splash = new SplashWindow();
            desktop.MainWindow = splash;
            splash.Show();

            _ = InitAfterSplashAsync(desktop, splash, mainViewModel, browseViewModel,
                installedViewModel, fileExplorerViewModel, toolsViewModel,
                settingsViewModel, authService, packageService, systemService,
                networkService, processService, performanceService, installService, sftpService, portalService,
                backgroundTaskService, notificationCenter, taskCenterViewModel, updateService);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OpenLoopbackExemptWizard(Window owner, XboxAuthService authService, SftpService sftpService, XboxPackageService packageService, bool quickMode)
    {
        var vm = new LoopbackExemptViewModel(authService, sftpService, packageService, quickMode);
        var win = new LoopbackExemptWindow { DataContext = vm };
        vm.OpenProjectLinkAction = () =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(LoopbackExemptViewModel.XFilesProjectUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OpenLoopbackExemptWizard: failed to open X-Files project link");
            }
        };
        vm.CloseAction = win.Close;
        win.Opened += async (_, _) => await vm.LoadCommand.ExecuteAsync(null);
        win.ShowDialog(owner);
    }

    private static void SetupGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Fatal(ex ?? new InvalidOperationException("Unknown"), "AppDomain unhandled exception");
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
        Logger.Error($"{title}: {description}\n{details}");

        if (Application.Current?.ApplicationLifetime is IActivityApplicationLifetime)
            return;

        try
        {
            _ = XBVault.Helpers.UIHelpers.RunOnUIAsync(async () =>
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
                catch (Exception ex)
                {
                    Logger.Error(ex, "ShowErrorDialogSafe: failed to show dialog");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ShowErrorDialogSafe: outer dispatch failed");
        }
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
        XboxAuthService authService,
        XboxPackageService packageService,
        XboxSystemService systemService,
        XboxNetworkService networkService,
        XboxProcessService processService,
        XboxPerformanceService performanceService,
        PackageInstallService installService,
        SftpService sftpService,
        PortalAppFilesService portalService,
        BackgroundTaskService backgroundTaskService,
        NotificationCenterService notificationCenter,
        TaskCenterViewModel taskCenterViewModel,
        InstalledAppUpdateService updateService)
    {
        Logger.Debug("Splash delay starting (2s)");
        await Task.Delay(SplashMinDelayMs);
        Logger.Debug("Splash delay complete, building main window");

        await XBVault.Helpers.UIHelpers.RunOnUIAsync(async () =>
        {
            var main = new MainWindow
            {
                DataContext = mainViewModel
            };

            desktop.MainWindow = main;
            main.Show();

            main.BindNotifications(notificationCenter);
            main.SetTaskCenter(taskCenterViewModel);

            main.Closed += (_, _) =>
            {
                main.UnbindNotifications();
                updateService.Stop();
                backgroundTaskService.Stop();
            };

            settingsViewModel.UiScaleChanged = () => main.ApplyUiScale();
            settingsViewModel.ShowLogsAction = () => mainViewModel.SelectedTab = 6;

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
                        if (browseViewModel.IsUpdateComplete)
                            _ = installedViewModel.RefreshPackagesCommand.ExecuteAsync(null);
                        browseViewModel.IsUpdateMode = false;
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
                var connVm = new ConnectionViewModel(authService, networkService);
                var connWindow = new Views.ConnectionWindow
                {
                    DataContext = connVm
                };
                await connWindow.ShowDialog(main);

                if (!connVm.IsSuccess && !connVm.IsCancelled)
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
                var refreshVm = new RefreshViewModel(new CatalogApiService(), async () =>
                {
                await XBVault.Helpers.UIHelpers.RunOnUIAsync(async () =>
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
                    "avares://XBVault/Assets/Views/ConfirmWindow/fluentui-collision-20.png",
                    "avares://XBVault/Assets/Views/ConfirmWindow/confirmwindow-exit-48.png",
                    isDestructive: true);
                var confirmWindow = new Views.ConfirmWindow { DataContext = confirmVm };
                await confirmWindow.ShowDialog(main);
                if (confirmVm.Confirmed)
                {
                    exitConfirmed = true;
                    main.Close();
                }
            };

            Logger.Info("Creating BrowseView");
            var browseView = new Views.BrowseView { DataContext = browseViewModel };
            Logger.Info("BrowseView created");

            installedViewModel.ConfirmUninstallAsync = async pkg =>
            {
                var confirmVm = new ConfirmViewModel(
                    "Uninstall Package",
                    $"Are you sure you want to uninstall {pkg.Name}?",
                    "Uninstall", "Cancel",
                    "avares://XBVault/Assets/Views/InstalledView/installed-uninstall-20.png",
                    "avares://XBVault/Assets/Views/ErrorDialog/errordialog-trash-48.png",
                    isDestructive: true);
                var confirmWindow = new Views.ConfirmWindow { DataContext = confirmVm };
                await confirmWindow.ShowDialog(main);
                return confirmVm.Confirmed;
            };

            installedViewModel.ConfirmAutostartAction = async (pkg, previousName) =>
            {
                var message = string.IsNullOrEmpty(previousName)
                    ? $"Launch {pkg.Name} automatically when XBVault connects to the Xbox?"
                    : $"Replace {previousName} with {pkg.Name} as the app that launches automatically on connect?";
                var confirmVm = new ConfirmViewModel(
                    "Autostart on Connect",
                    message,
                    "Enable", "Cancel",
                    "avares://XBVault/Assets/Views/InstalledView/installed-autostart-20.png",
                    "avares://XBVault/Assets/Views/InstalledView/installed-autostart-48.png");
                var confirmWindow = new Views.ConfirmWindow { DataContext = confirmVm };
                await confirmWindow.ShowDialog(main);
                return confirmVm.Confirmed;
            };

            installedViewModel.NotifyAutostartAction = message =>
            {
                notificationCenter.Notify(
                    "Autostart",
                    message,
                    "avares://XBVault/Assets/Views/InstalledView/installed-autostart-16.png");
            };

            toolsViewModel.ShowConnectAction = async () =>
            {
                var connVm = new ConnectionViewModel(authService, networkService);
                var connWindow = new Views.ConnectionWindow { DataContext = connVm };
                await connWindow.ShowDialog(main);
                return connVm.IsSuccess;
            };

            fileExplorerViewModel.ShowConnectAction = async () =>
            {
                var connVm = new ConnectionViewModel(authService, networkService);
                var connWindow = new Views.ConnectionWindow { DataContext = connVm };
                await connWindow.ShowDialog(main);
                return connVm.IsSuccess;
            };

            installedViewModel.ShowConnectAction = async () =>
            {
                var connVm = new ConnectionViewModel(authService, networkService);
                var connWindow = new Views.ConnectionWindow { DataContext = connVm };
                await connWindow.ShowDialog(main);
                return connVm.IsSuccess;
            };

            installedViewModel.ShowErrorAction = async (title, description, details) =>
            {
                var errDlg = new ErrorDialog(title, description, details, ErrorDialogType.Warn);
                await errDlg.ShowDialog(main);
            };

            installedViewModel.ShowErrorWithConnectAction = async (title, description, details, connectAction) =>
            {
                var errDlg = new ErrorDialog(title, description, details, ErrorDialogType.Warn)
                {
                    ConnectAction = connectAction
                };
                await errDlg.ShowDialog(main);
            };

            installedViewModel.ResolveBannerAsync = pkg => browseViewModel.FindThumbnailByPackageAsync(pkg);
            installedViewModel.CheckOutdatedAsync = async pkg =>
            {
                var result = browseViewModel.FindCatalogMatch(pkg);
                return result;
            };
            installedViewModel.RescanUpdatesAction = () => updateService.ScanAsync();
            installedViewModel.ShowCatalogDetailAction = catalogItem =>
            {
                browseViewModel.IsUpdateMode = true;
                browseViewModel.SelectedItem = catalogItem;
            };
            updateService.OpenUpdateDialogAsync = catalogItem =>
            {
                browseViewModel.IsUpdateMode = true;
                browseViewModel.SelectedItem = catalogItem;
                return Task.CompletedTask;
            };
            updateService.Start();
            browseViewModel.OnCatalogLoaded = () =>
            {
                if (installedViewModel is not null)
                    installedViewModel.IsCatalogReady = true;
            };

            mainViewModel.OnTabChanged = tab =>
            {
                if (tab == 1)
                {
                    installedViewModel.StartPolling();
                    if (authService.IsConnected)
                        _ = installedViewModel.RefreshPackagesCommand.ExecuteAsync(null);
                }
                else
                {
                    installedViewModel.StopPolling();
                }
            };

            Logger.Info("Creating InstalledView");
            var installedView = new Views.InstalledView { DataContext = installedViewModel };
            Logger.Info("InstalledView created");
            settingsViewModel.ShowConnectDialogAsync = async () =>
            {
                var connVm = new ConnectionViewModel(authService, networkService);
                var connWindow = new Views.ConnectionWindow { DataContext = connVm };
                await connWindow.ShowDialog(main);
                return connVm.IsSuccess;
            };

            Logger.Info("Creating FileExplorerView");
            var fileExplorerView = new Views.FileExplorerView();
            Logger.Info("Setting FileExplorerView DataContext");
            fileExplorerView.DataContext = fileExplorerViewModel;
            Logger.Info("FileExplorerView created");
            Logger.Info("Creating ToolsView");
            var toolsView = new Views.ToolsView { DataContext = toolsViewModel };
            Logger.Info("ToolsView created");

            toolsViewModel.ShowScreenshotAction = () =>
            {
                var vm = new ScreenshotViewModel(systemService);
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
                var vm = new SystemInfoViewModel(authService, systemService);
                var win = new Views.SystemInfoWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.OpenLoopbackExemptAction = () => OpenLoopbackExemptWizard(main, authService, sftpService, packageService, quickMode: false);
            toolsViewModel.OpenLoopbackExemptQuickAction = () => OpenLoopbackExemptWizard(main, authService, sftpService, packageService, quickMode: true);

            toolsViewModel.ShowProcessesAction = () =>
            {
                var vm = new ProcessesViewModel(processService);
                var win = new Views.ProcessesWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowNetworkInfoAction = () =>
            {
                var vm = new NetworkInfoViewModel(networkService);
                var win = new Views.NetworkInfoWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowPerformanceAction = () =>
            {
                var vm = new PerformanceViewModel(authService, performanceService);
                var win = new Views.PerformanceWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowCrashDataAction = () =>
            {
                var vm = new CrashDataViewModel(authService, systemService);
                var win = new Views.CrashDataWindow { DataContext = vm };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowUsbPermissionAction = () =>
            {
                var vm = new UsbPermissionViewModel();
                var win = new Views.UsbPermissionWindow { DataContext = vm };
                vm.CloseAction = () => win.Close();
                win.Opened += async (_, _) =>
                {
                    await vm.LoadDrivesCommand.ExecuteAsync(null);
                };
                win.ShowDialog(main);
            };

            toolsViewModel.ShowInfoAsync = async (title, desc, details) =>
            {
                var dlg = new ErrorDialog(title, desc, details, ErrorDialogType.Info);
                await dlg.ShowDialog(main);
            };

            Action openCustomInstall = () =>
            {
                if (!authService.IsConnected)
                {
                    var errDlg = new ErrorDialog(
                        "Not Connected",
                        "Connect to an Xbox first before using Custom Install.",
                        "Go to the sidebar and connect to your Xbox Developer Mode console.",
                        ErrorDialogType.Warn);
                    errDlg.ConnectAction = () => mainViewModel.ConnectCommand.ExecuteAsync(null);
                    errDlg.ShowDialog(main);
                    return;
                }
                var vm = new CustomInstallViewModel(packageService, installService);
                vm.OnInstallComplete = () => installedViewModel.RefreshPackagesCommand.Execute(null);
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
                        return files is { Count: > 0 } ? files[0].TryGetLocalPath() : null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "CustomInstall file picker failed");
                        return null;
                    }
                };
                vm.PickDependencyFilesAsync = async () =>
                {
                    try
                    {
                        var files = await main.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                        {
                            Title = "Select Dependencies",
                            AllowMultiple = true,
                            FileTypeFilter =
                            [
                                new FilePickerFileType("Package files")
                                {
                                    Patterns = ["*.appx", "*.msix", "*.appxbundle", "*.msixbundle", "*.zip"]
                                }
                            ]
                        });
                        return files?.Select(f => f.TryGetLocalPath())
                                     .Where(p => p is not null)
                                     .Cast<string>()
                                     .ToArray();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "CustomInstall dependency picker failed");
                        return null;
                    }
                };
                var win = new Views.CustomInstallWindow { DataContext = vm };
                vm.CloseAction = () => win.Close();
                win.ShowDialog(main);
            };
            toolsViewModel.ShowCustomInstallAction = openCustomInstall;
            browseViewModel.ShowCustomInstallAction = openCustomInstall;
            installedViewModel.ShowCustomInstallAction = openCustomInstall;

                Func<string, Task> openCustomInstallWithFile = async (filePath) =>
            {
                if (!authService.IsConnected)
                {
                    var errDlg = new ErrorDialog(
                        "Not Connected",
                        "Connect to an Xbox first before using Custom Install.",
                        "Go to the sidebar and connect to your Xbox Developer Mode console.",
                        ErrorDialogType.Warn);
                    errDlg.ConnectAction = () => mainViewModel.ConnectCommand.ExecuteAsync(null);
                    await errDlg.ShowDialog(main);
                    return;
                }
                var vm = new CustomInstallViewModel(packageService, installService);
                vm.PickFileAsync = () => Task.FromResult<string?>(null);
                vm.PickDependencyFilesAsync = async () =>
                {
                    try
                    {
                        var files = await main.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                        {
                            Title = "Select Dependencies",
                            AllowMultiple = true,
                            FileTypeFilter =
                            [
                                new FilePickerFileType("Package files")
                                {
                                    Patterns = ["*.appx", "*.msix", "*.appxbundle", "*.msixbundle", "*.zip"]
                                }
                            ]
                        });
                        return files?.Select(f => f.TryGetLocalPath())
                                     .Where(p => p is not null)
                                     .Cast<string>()
                                     .ToArray();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "CustomInstall dependency picker failed");
                        return null;
                    }
                };
                vm.SourcePath = filePath;
                var win = new Views.CustomInstallWindow { DataContext = vm };
                vm.CloseAction = () => win.Close();
                win.Opened += async (_, _) =>
                {
                    await vm.AnalyzeCommand.ExecuteAsync(null);
                };
                await win.ShowDialog(main);
                installedViewModel.RefreshPackagesCommand.Execute(null);
            };
            browseViewModel.OpenCustomInstallWithFileAction = openCustomInstallWithFile;
            installedViewModel.OpenCustomInstallWithFileAction = openCustomInstallWithFile;
            fileExplorerViewModel.OpenCustomInstallWithFileAction = openCustomInstallWithFile;

            fileExplorerViewModel.ShowConfirmAction = async (title, message, confirmText, cancelText) =>
            {
                var vm = new ConfirmViewModel(title, message, confirmText, cancelText, null, null);
                var win = new Views.ConfirmWindow { DataContext = vm };
                await win.ShowDialog(main);
                return vm.Confirmed;
            };

            toolsViewModel.ShowConfirmAsync = async (title, message, confirmText, cancelText, iconSource, messageIconSource) =>
            {
                var vm = new ConfirmViewModel(title, message, confirmText, cancelText, iconSource, messageIconSource);
                var win = new Views.ConfirmWindow { DataContext = vm };
                await win.ShowDialog(main);
                return vm.Confirmed;
            };

            settingsViewModel.ShowConfirmAsync = async (title, message, confirmText, cancelText, iconSource, messageIconSource) =>
            {
                var vm = new ConfirmViewModel(title, message, confirmText, cancelText, iconSource, messageIconSource);
                var win = new Views.ConfirmWindow { DataContext = vm };
                await win.ShowDialog(main);
                return vm.Confirmed;
            };

            Logger.Info("Creating InspectorView");
            var agentService = new XrayAgentService();
            var inspectorViewModel = new InspectorViewModel(authService, agentService);
            inspectorViewModel.ShowConnectAction = mainViewModel.ShowConnectAction;
            inspectorViewModel.ShowGuideAction = () =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://xbvault.pages.dev/inspector") { UseShellExecute = true });
            inspectorViewModel.ShowConfirmAsync = async (title, message, confirmText, cancelText, iconSource, messageIconSource) =>
            {
                var vm = new ConfirmViewModel(title, message, confirmText, cancelText, iconSource, messageIconSource);
                var win = new Views.ConfirmWindow { DataContext = vm };
                await win.ShowDialog(main);
                return vm.Confirmed;
            };
            inspectorViewModel.ShowSaveFileDialogAsync = async (suggestedName) =>
            {
                var topLevel = TopLevel.GetTopLevel(main);
                if (topLevel is null) return null;
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    SuggestedFileName = suggestedName
                });
                return file?.TryGetLocalPath();
            };
            inspectorViewModel.ShowInputPromptAsync = async (title, message, defaultValue, iconUri) =>
            {
                var dlg = new Views.InputDialog(title, message, defaultValue, iconUri);
                await dlg.ShowDialog(main);
                return dlg.Value;
            };
            inspectorViewModel.OpenCustomInstallWithFileAction = openCustomInstallWithFile;
            var inspectorView = new Views.InspectorView { DataContext = inspectorViewModel };

            Logger.Info("Creating SettingsView");
            var settingsView = new Views.SettingsView { DataContext = settingsViewModel };
            Logger.Info("Creating LogsView");
            var logsView = new Views.LogsView { DataContext = new LogsViewModel() };

            main.ViewCarousel.Items.Add(browseView);
            main.ViewCarousel.Items.Add(installedView);
            main.ViewCarousel.Items.Add(fileExplorerView);
            main.ViewCarousel.Items.Add(toolsView);
            main.ViewCarousel.Items.Add(inspectorView);
            main.ViewCarousel.Items.Add(settingsView);
            main.ViewCarousel.Items.Add(logsView);

            // kick off background loads
            _ = browseViewModel.LoadCatalogCommand.ExecuteAsync(null);
            // Installed packages loaded only on explicit refresh (manual connect)

            // File explorer: manual init via Browse button

            Logger.Info("Main window loaded, closing splash");
            splash.Close();

            // First-run wizard (after splash to avoid z-order overlap)
            if (!SettingsService.Current.XboxConnection.IsConfigured)
            {
                var wizardVm = new SetupWizardViewModel(authService);
                var wizardWin = new Views.SetupWizardWindow { DataContext = wizardVm };
                wizardVm.CloseAction = () => wizardWin.Close();
                await wizardWin.ShowDialog(main);
                if (wizardVm.WasCompleted && wizardVm.OpenConnectionAfter && mainViewModel.ShowConnectAction is not null)
                {
                    var connected = await mainViewModel.ShowConnectAction();
                    if (connected)
                    {
                        await authService.FetchSmbPasswordAsync();
                        mainViewModel.IsXboxConnected = true;
                        authService.MarkConnected();
                        mainViewModel.ConnectionStatusText = "Connected";
                    }
                }
            }

            // Autoconnect on start (visible in task center) — only when enabled and credentials present
            if (SettingsService.Current.AutoConnect && authService.IsConfigured && !authService.IsConnected)
            {
                var connectTask = backgroundTaskService.RunAsync("Connecting to Xbox…",
                    async (task, ct) =>
                    {
                        var ok = await authService.EnsureConnectedAsync(ct);
                        if (!ok)
                            throw new InvalidOperationException("Could not connect to the Xbox console. Check that it's powered on and on the same network.");
                    });
                _ = NotifyAutoconnectResultAsync(notificationCenter, connectTask);
            }

            // Auto-update check
            _ = CheckForUpdatesAsync(main);
        });
    }

    private static async Task NotifyAutoconnectResultAsync(NotificationCenterService notifications, BackgroundTask task)
    {
        while (!task.IsFinished)
            await Task.Delay(100);
        if (task.IsFailed)
            notifications.Notify("Autoconnect failed", "Could not connect to the Xbox console.",
                "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-error-20.png");
        else if (task.Status == BackgroundTaskStatus.Succeeded)
            notifications.Notify("Connected", "Connected to the Xbox console.",
                "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-success-20.png");
    }

    private static async Task CheckForUpdatesAsync(Window main)
    {
        try
        {
            using var checker = new GitHubReleaseCheckerService();
            var release = await checker.CheckLatestReleaseAsync();
            if (release is null) return;
            if (!GitHubReleaseCheckerService.IsNewerVersion(release.TagName, BuildInfo.Version))
                return;

            Logger.Info($"Update available: {release.TagName} (current: {BuildInfo.Version})");
            var dlg = new Views.ErrorDialog(
                "Update Available",
                $"XB Homebrew Vault {release.TagName} is available. You are currently running {BuildInfo.DisplayVersion}.",
                "Visit the releases page to download the latest version.",
                Views.ErrorDialogType.Info);
            dlg.DownloadAction = () =>
            {
                Process.Start(new ProcessStartInfo(release.HtmlUrl ?? "https://github.com/marcelofrau/xb-homebrew-vault/releases") { UseShellExecute = true });
                dlg.Close();
                return Task.CompletedTask;
            };
            await dlg.ShowDialog(main);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Auto-update check failed");
        }
    }

    private static void LogGpuInfo()
    {
        // Can't query the active renderer at App init in Avalonia 12.
        // The configured backend is logged here; actual GPU/software
        // fallback info is available once a TopLevel window exists.
        Logger.Info("Rendering: Skia via ANGLE (D3D11), MaxGpuResourceSizeBytes=512MB, UseRegionDirtyRectClipping=true");
    }

    private static async Task InitAndroidAfterSplashAsync(
        Panel rootPanel,
        Views.MobileSplashView splash)
    {
        try
        {
            // Wait for splash to actually render before starting countdown
            var splashRendered = new TaskCompletionSource();
            splash.Loaded += (_, _) => splashRendered.TrySetResult();
            await splashRendered.Task;
            Logger.Debug($"Android splash delay starting ({SplashMinDelayMs}ms) @ {DateTime.Now:HH:mm:ss.fff}");
            await Task.Delay(SplashMinDelayMs);
            Logger.Debug($"Android splash delay complete @ {DateTime.Now:HH:mm:ss.fff}");

        // Initialize all services (same as desktop)
        var authService = new XboxAuthService();
        var packageService = new XboxPackageService(authService);
        var systemService = new XboxSystemService(authService);
        var networkService = new XboxNetworkService(authService);
        var processService = new XboxProcessService(authService);
        var performanceService = new XboxPerformanceService(authService);
        var cacheService = new CacheService();
        var installService = new PackageInstallService(cacheService, packageService);
        var sftpService = new SftpService();
        var sftpTransferService = new SftpTransferService(sftpService);
        var portalService = new PortalAppFilesService(authService, packageService);
        var catalogService = new CatalogApiService();
        var overrideService = new PackageOverrideService();
        overrideService.Initialize();
        var versionChecker = new VersionCheckerService(overrideService);
        var backgroundTaskService = new BackgroundTaskService();
        backgroundTaskService.Start();
        var notificationCenter = new NotificationCenterService();
        var taskCenterViewModel = new TaskCenterViewModel(backgroundTaskService);

        var mainViewModel = new MainViewModel(authService);
        var browseViewModel = new BrowseViewModel(installService, authService, packageService, catalogService, overrideService, versionChecker);
        var installedViewModel = new InstalledViewModel(authService, packageService);
        var fileExplorerViewModel = new FileExplorerViewModel(authService, sftpService, sftpTransferService, portalService);
        var toolsViewModel = new ToolsViewModel(authService, systemService);
        var settingsViewModel = new SettingsViewModel(authService, cacheService);

        Logger.Debug($"Android services initialized, switching to MobileMainWindow @ {DateTime.Now:HH:mm:ss.fff}");

        await XBVault.Helpers.UIHelpers.RunOnUIAsync(async () =>
        {
            var main = new Views.MobileMainWindow();
            main.SetDataContext(mainViewModel);
            main.BrowseContent.DataContext = browseViewModel;
            main.ToolsContent.DataContext = toolsViewModel;

            // ── Mobile tools: wire action delegates ──

            // Helper: open a text-based tool overlay, fetch data on background thread
            void ShowToolOverlay(string title, Func<Task<string?>> loadData)
            {
                var vm = new Views.MobileToolResultViewModel { IsLoading = true };
                var view = new Views.MobileToolResultView { DataContext = vm };
                var overlay = new Views.MobileToolOverlayView();
                overlay.SetTitle(title);
                overlay.SetContent(view);
                overlay.SetOnBack(() => main.CloseOverlay());
                main.ShowOverlay(overlay);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var text = await loadData();
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            vm.ContentText = text ?? "(no data)";
                            vm.IsLoading = false;
                        });
                    }
                    catch (Exception ex)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            vm.StatusMessage = $"Failed to load: {ex.Message}";
                            vm.IsLoading = false;
                        });
                    }
                });
            }

            toolsViewModel.ShowScreenshotAction = () =>
            {
                var vm = new Views.MobileScreenshotViewModel(systemService);
                vm.SaveScreenshotDialog = async stream =>
                {
                    var result = new TaskCompletionSource<string?>();
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var topLevel = TopLevel.GetTopLevel(main)!;
                        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                        {
                            Title = "Save Screenshot",
                            SuggestedFileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                            FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } }
                        });
                        result.SetResult(file?.TryGetLocalPath());
                    });
                    var path = await result.Task;
                    if (!string.IsNullOrEmpty(path))
                    {
                        using var fs = File.Create(path);
                        stream.Position = 0;
                        await stream.CopyToAsync(fs);
                    }
                    return path;
                };
                var sView = new Views.MobileScreenshotView { DataContext = vm };
                var overlay = new Views.MobileToolOverlayView();
                overlay.SetTitle("Screenshot");
                overlay.SetContent(sView);
                overlay.SetOnBack(() => { vm.Dispose(); main.CloseOverlay(); });
                main.ShowOverlay(overlay);
            };

            toolsViewModel.ShowSystemInfoAction = () =>
                ShowToolOverlay("System Info", () => systemService.GetSystemInfoAsync());

            toolsViewModel.ShowCrashDataAction = () =>
                ShowToolOverlay("Crash Data", () => systemService.GetCrashDumpsAsync());

            toolsViewModel.ShowProcessesAction = () =>
                ShowToolOverlay("Processes", () => processService.GetProcessesAsync());

            toolsViewModel.ShowNetworkInfoAction = () =>
                ShowToolOverlay("Network Info", () => networkService.GetNetworkConfigAsync());

            toolsViewModel.ShowPerformanceAction = async () =>
            {
                var vm = new Views.MobileToolResultViewModel { IsLoading = true };
                var view = new Views.MobileToolResultView { DataContext = vm };
                var overlay = new Views.MobileToolOverlayView();
                overlay.SetTitle("Performance");
                overlay.SetContent(view);
                overlay.SetOnBack(() => main.CloseOverlay());
                main.ShowOverlay(overlay);

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await performanceService.ConnectPerformanceWsAsync(
                        snap =>
                        {
                            var b = new System.Text.StringBuilder();
                            b.AppendLine($"CPU: {snap.CpuLoad}%");
                            b.AppendLine($"GPU: {snap.GpuUsage}%");
                            b.AppendLine($"Memory Used: {snap.MemoryUsedMB:F1} MB");
                            b.AppendLine($"Memory Total: {snap.MemoryTotalMB:F1} MB");
                            b.AppendLine($"Committed: {snap.MemoryCommittedBytes / 1024 / 1024} MB");
                            b.AppendLine($"IO Read: {snap.IOReadSpeed / 1024} KB/s  Write: {snap.IOWriteSpeed / 1024} KB/s");
                            b.AppendLine($"Net In: {snap.NetworkInBytes / 1024} KB  Out: {snap.NetworkOutBytes / 1024} KB");
                            _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                vm.ContentText = b.ToString();
                                vm.IsLoading = false;
                            });
                        }, cts.Token);

                    // If WS ended without data
                    if (vm.IsLoading)
                    {
                        vm.StatusMessage = "Performance data not available — WebSocket may not be supported";
                        vm.IsLoading = false;
                    }
                }
                catch (Exception ex)
                {
                    vm.StatusMessage = $"Performance failed: {ex.Message}";
                    vm.IsLoading = false;
                }
            };

            toolsViewModel.ShowUsbPermissionAction = async () =>
            {
                if (toolsViewModel.ShowInfoAsync is not null)
                {
                    await toolsViewModel.ShowInfoAsync(
                        "Windows Only",
                        "USB Media Drive activation is currently only available on Windows.",
                        "We're evaluating support for Android. Stay tuned for future updates!");
                }
            };

            toolsViewModel.OpenLoopbackExemptAction = () =>
            {
                var vm = new LoopbackExemptViewModel(authService, sftpService, packageService);
                vm.ShowConfirmAsync = toolsViewModel.ShowConfirmAsync;
                vm.CloseAction = () => main.CloseOverlay();
                vm.OpenProjectLinkAction = () =>
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LoopbackExemptViewModel.XFilesProjectUrl) { UseShellExecute = true });
                var lView = new Views.MobileLoopbackView();
                lView.SetViewModel(vm);
                var overlay = new Views.MobileToolOverlayView();
                overlay.SetTitle("Loopback Exempt");
                overlay.SetContent(lView);
                overlay.SetOnBack(() => main.CloseOverlay());
                main.ShowOverlay(overlay);
                _ = vm.LoadCommand.ExecuteAsync(null);
            };

            toolsViewModel.OpenLoopbackExemptQuickAction = () =>
            {
                var vm = new LoopbackExemptViewModel(authService, sftpService, packageService, quickMode: true);
                vm.ShowConfirmAsync = toolsViewModel.ShowConfirmAsync;
                vm.CloseAction = () => main.CloseOverlay();
                vm.OpenProjectLinkAction = () =>
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LoopbackExemptViewModel.XFilesProjectUrl) { UseShellExecute = true });
                var lView = new Views.MobileLoopbackView();
                lView.SetViewModel(vm);
                var overlay = new Views.MobileToolOverlayView();
                overlay.SetTitle("X-Files Enablement");
                overlay.SetContent(lView);
                overlay.SetOnBack(() => main.CloseOverlay());
                main.ShowOverlay(overlay);
                _ = vm.LoadCommand.ExecuteAsync(null);
            };

            toolsViewModel.ShowCustomInstallAction = () => ShowMobileCustomInstall(main, packageService, installService);
            browseViewModel.ShowCustomInstallAction = () => ShowMobileCustomInstall(main, packageService, installService);

            // OpenDevPortalCommand is auto-generated by [RelayCommand] — uses Process.Start,
            // which works on Android (opens in default browser).

            // Confirm / Info dialogs (from ToolsViewModel delegates)
            toolsViewModel.ShowConfirmAsync = async (title, message, confirmText, cancelText, iconSource, messageIconSource) =>
            {
                var vm = new Views.MobileConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = confirmText ?? "OK",
                    CancelText = cancelText ?? "Cancel"
                };
                var dlg = new Views.MobileConfirmDialogView { DataContext = vm };
                var tcs = vm.WaitForResult();
                main.ShowOverlay(dlg);
                dlg.SetOnBack(() => { vm.CancelCommand.Execute(null); main.CloseOverlay(); });
                var result = await tcs;
                main.CloseOverlay();
                return result;
            };

            toolsViewModel.ShowInfoAsync = async (title, description, details) =>
            {
                var vm = new Views.MobileInfoDialogViewModel
                {
                    Title = title,
                    Description = description ?? "",
                    Details = details
                };
                var dlg = new Views.MobileInfoDialogView { DataContext = vm };
                var tcs = vm.WaitForResult();
                main.ShowOverlay(dlg);
                dlg.SetOnBack(() => { vm.OkCommand.Execute(null); main.CloseOverlay(); });
                await tcs;
                main.CloseOverlay();
            };

            main.SettingsViewModel = settingsViewModel;
            main.NotificationCenter = notificationCenter;
            main.BackgroundTasks = backgroundTaskService;
            main.AuthService = authService;

            rootPanel.Children.Remove(splash);
            rootPanel.Children.Add(main);

            mainViewModel.ShowConnectAction = async () =>
            {
                Logger.Info("Android: ShowConnectAction invoked — opening MobileConnectionView");
                var tcs = new TaskCompletionSource<bool>();
                var connVm = new ConnectionViewModel(authService, networkService);
                var connView = new Views.MobileConnectionView { DataContext = connVm };
                connVm.Completed += success =>
                {
                    Logger.Info($"Android: Connection completed: success={success}");
                    tcs.SetResult(success);
                };
                connView.SetOnBack(() =>
                {
                    if (connVm.IsRunning)
                        connVm.CancelCommand.Execute(null);
                    main.CloseOverlay();
                    if (!tcs.Task.IsCompleted)
                        tcs.SetResult(false);
                });
                main.ShowOverlay(connView);
                _ = connVm.ConnectCommand.ExecuteAsync(null);
                return await tcs.Task;
            };

            mainViewModel.ShowAboutAction = () =>
            {
                Logger.Info("Android: ShowAboutAction invoked");
            };

            settingsViewModel.ShowLogsAction = () =>
            {
                Logger.Info("Android: ShowLogsAction invoked");
            };

            browseViewModel.ShowDetailAction = item =>
            {
                Logger.Info($"Android: ShowDetailAction for {item.Name}");
                var detail = new Views.MobileDetailView { DataContext = browseViewModel };
                detail.SetOnBack(() =>
                {
                    main.CloseOverlay();
                    if (browseViewModel.IsUpdateComplete)
                        _ = installedViewModel.RefreshPackagesCommand.ExecuteAsync(null);
                    browseViewModel.IsUpdateMode = false;
                    browseViewModel.SelectedItem = null;
                });
                browseViewModel.CloseDetailAction = () => main.CloseOverlay();
                main.ShowOverlay(detail);
            };

            mainViewModel.OnTabChanged = tab =>
            {
                Logger.Info($"Android: OnTabChanged → tab {tab}");
                if (tab == 1)
                {
                    installedViewModel.StartPolling();
                    if (authService.IsConnected)
                        _ = installedViewModel.RefreshPackagesCommand.ExecuteAsync(null);
                }
                else
                {
                    installedViewModel.StopPolling();
                }
            };

            Logger.Info($"Android: MobileMainWindow loaded @ {DateTime.Now:HH:mm:ss.fff}");

            // Auto-load catalog on startup (same as desktop)
            _ = browseViewModel.LoadCatalogCommand.ExecuteAsync(null);

            // First-run wizard on Android
            if (!SettingsService.Current.WizardCompleted && !SettingsService.Current.XboxConnection.IsConfigured)
            {
                Logger.Info("Android: Settings not configured, showing setup wizard");
                var wizardVm = new SetupWizardViewModel(authService);
                var wizardView = new Views.MobileSetupWizardView();
                wizardView.SetViewModel(wizardVm);
                wizardVm.CloseAction = () => main.CloseOverlay();
                wizardView.CloseRequested += async (_, _) =>
                {
                    main.CloseOverlay();
                    if (wizardVm.WasCompleted)
                    {
                        Logger.Info("Android: Setup wizard completed");
                        if (wizardVm.OpenConnectionAfter && mainViewModel.ShowConnectAction is not null)
                        {
                            var connected = await mainViewModel.ShowConnectAction();
                            if (connected)
                            {
                                await authService.FetchSmbPasswordAsync();
                                mainViewModel.IsXboxConnected = true;
                                authService.MarkConnected();
                                mainViewModel.ConnectionStatusText = "Connected";
                            }
                        }
                        mainViewModel.UpdateConnectionStatus();
                    }
                };
                main.ShowOverlay(wizardView);
            }
        });
        }
        catch (Exception ex)
        {
            var fullMsg = ex.InnerException != null
                ? $"{ex.Message}\n\nInner: {ex.InnerException.Message}"
                : ex.Message;
            Logger.Error($"Android splash transition failed: {fullMsg}");
            // Still try to show something — replace splash with error
            await XBVault.Helpers.UIHelpers.RunOnUIAsync(async () =>
            {
                rootPanel.Children.Remove(splash);
                rootPanel.Children.Add(new TextBlock
                {
                    Text = $"Android init failed:\n{fullMsg}",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Red),
                    FontSize = 12,
                    Margin = new Thickness(16),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                });
            });
        }
    }

    private static void ShowMobileCustomInstall(
        Views.MobileMainWindow main,
        IXboxPackageService packageService,
        PackageInstallService installService)
    {
        var vm = new CustomInstallViewModel(packageService, installService);
        var pickFileFilter = new List<FilePickerFileType>
        {
            new FilePickerFileType("Package files")
            {
                Patterns = new[] { "*.appx", "*.msix", "*.appxbundle", "*.msixbundle", "*.zip" }
            }
        };
        vm.PickFileAsync = async () =>
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(main)!;
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Package",
                    AllowMultiple = false,
                    FileTypeFilter = pickFileFilter
                });
                return files is { Count: > 0 } ? files[0].TryGetLocalPath() : null;
            }
            catch { return null; }
        };
        vm.PickDependencyFilesAsync = async () =>
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(main)!;
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Dependencies",
                    AllowMultiple = true,
                    FileTypeFilter = pickFileFilter
                });
                return files?.Select(f => f.TryGetLocalPath())
                             .Where(p => p is not null)
                             .Cast<string>()
                             .ToArray();
            }
            catch { return null; }
        };
        vm.CloseAction = () => main.CloseOverlay();
        var ciView = new Views.MobileCustomInstallView();
        ciView.SetViewModel(vm);
        ciView.CloseRequested += (_, _) => main.CloseOverlay();
        main.ShowOverlay(ciView);
    }
}
