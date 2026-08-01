using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Diagnostics;
using XBVault.Helpers;
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
        if (Program.PreFlightReport is { } report)
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

        LogGpuInfo();

        Logger.Info("Application initialized");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
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
            var catalogService = new CatalogApiService();
            var overrideService = new PackageOverrideService();
            overrideService.Initialize();

            var mainViewModel = new MainViewModel(authService);
            var browseViewModel = new BrowseViewModel(installService, authService, packageService, catalogService, overrideService);
            var installedViewModel = new InstalledViewModel(authService, packageService);
            var fileExplorerViewModel = new FileExplorerViewModel(authService, sftpService, sftpTransferService);
            var toolsViewModel = new ToolsViewModel(authService, systemService);
            var settingsViewModel = new SettingsViewModel(authService, cacheService);

            // splash first, main after delay
            var splash = new SplashWindow();
            splash.Show();

            _ = InitAfterSplashAsync(desktop, splash, mainViewModel, browseViewModel,
                installedViewModel, fileExplorerViewModel, toolsViewModel,
                settingsViewModel, authService, packageService, systemService,
                networkService, processService, performanceService, installService);
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
                catch (Exception ex)
                {
                    Logger.Error(ex, "ShowErrorDialogSafe: failed to show dialog");
                }
            }, DispatcherPriority.Send);
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
        PackageInstallService installService)
    {
        Logger.Debug("Splash delay starting (2s)");
        await Task.Delay(SplashMinDelayMs);
        Logger.Debug("Splash delay complete, building main window");

        await Dispatcher.UIThread.InvokeAsync(async () =>
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
            installedViewModel.ShowCatalogDetailAction = catalogItem =>
            {
                browseViewModel.IsUpdateMode = true;
                browseViewModel.SelectedItem = catalogItem;
            };
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

            // Auto-update check
            _ = CheckForUpdatesAsync(main);
        });
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
}
