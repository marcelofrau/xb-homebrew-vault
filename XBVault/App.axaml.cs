#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using XBVault.Helpers;
using XBVault.Models;
using XBVault.Services;
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
        Logger.WriteSessionHeader();

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
            "Info" => LogLevel.Info,
            "Warn" => LogLevel.Warn,
            "Error" => LogLevel.Error,
            "Fatal" => LogLevel.Fatal,
            _ => LogLevel.Info
        };
        Logger.Debug($"Log level initialized to {savedLevel}");

        if (ApplicationLifetime is IActivityApplicationLifetime)
            Logger.MinLevel = LogLevel.Trace;

        LogGpuInfo();

        Logger.Info("Application initialized");

        if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            // Android: root panel holds splash initially, swaps to main after init
            Logger.Debug($"Android: IActivityApplicationLifetime detected @ {DateTime.Now:HH:mm:ss.fff}");
            var rootPanel = new Panel();
            var splash = new MobileSplashView();
            rootPanel.Children.Add(splash);
            Logger.Debug($"Android: splash added to rootPanel @ {DateTime.Now:HH:mm:ss.fff}");
            activity.MainViewFactory = () =>
            {
                Logger.Debug($"Android: MainViewFactory called @ {DateTime.Now:HH:mm:ss.fff}");
                return rootPanel;
            };
            Logger.Debug($"Android: MainViewFactory set, launching init @ {DateTime.Now:HH:mm:ss.fff}");

            var services = AppServices.Create();
            services.Initialize();
            SftpEntry.IconViewFolder = "MobileFileExplorer";
            SftpEntry.IconFilePrefix = "mobilefileexplorer";
            SftpEntry.IconSizeSuffix = "32";

            _ = InitAndroidAfterSplashAsync(rootPanel, splash, services);
        }
        else if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = AppServices.Create();
            services.Initialize();

            // splash first, main after delay
            var splash = new SplashWindow();
            desktop.MainWindow = splash;
            splash.Show();

            _ = InitAfterSplashAsync(desktop, splash, services);
        }

        base.OnFrameworkInitializationCompleted();
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
            ShowErrorDialogSafe("Task Error", "An unhandled error occurred in a background task.", e.Exception.ToString(), ErrorDialogType.Error);
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

    internal static void ShowErrorDialogSafe(string title, string description, string details, ErrorDialogType type)
    {
        Logger.Error($"{title}: {description}\n{details}");

        try
        {
            _ = UIHelpers.RunOnUIAsync(async () =>
            {
                try
                {
                    if (Application.Current?.ApplicationLifetime is IActivityApplicationLifetime)
                    {
                        var mobileDlg = new MobileErrorDialogView
                        {
                            DataContext = new MobileErrorDialogViewModel
                            {
                                Title = title,
                                Description = description,
                                Details = details,
                                DialogType = type
                            }
                        };
                        mobileDlg.OkClicked += (_, _) =>
                        {
                            if (TopLevel.GetTopLevel(mobileDlg)?.Content is Panel panel)
                                panel.Children.Remove(mobileDlg);
                        };
                        if (TopLevel.GetTopLevel(mobileDlg)?.Content is Panel targetPanel)
                            targetPanel.Children.Add(mobileDlg);
                        return;
                    }

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
        AppServices services)
    {
        Logger.Debug("Splash delay starting (2s)");
        await Task.Delay(SplashMinDelayMs);
        Logger.Debug("Splash delay complete, building main window");

        await UIHelpers.RunOnUIAsync(async () =>
        {
            var main = new MainWindow
            {
                DataContext = services.Main
            };

            desktop.MainWindow = main;
            main.Show();
            Logger.Debug("Desktop: UI wiring starting");

            var ui = new DesktopUiActions(services, main);
            ui.Wire();

            // kick off background loads
            _ = services.Browse.LoadCatalogCommand.ExecuteAsync(null);
            // Installed packages loaded only on explicit refresh (manual connect)

            // File explorer: manual init via Browse button

            Logger.Debug("Main window loaded, closing splash");
            splash.Close();

            // First-run wizard, undecryptable-password prompt and autoconnect
            await ui.RunStartupAsync();
            Logger.Debug("Desktop: startup sequence complete");

            // Auto-update check
            _ = CheckForUpdatesAsync(main);
        });
    }

    private static async Task InitAndroidAfterSplashAsync(
        Panel rootPanel,
        MobileSplashView splash,
        AppServices services)
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

            Logger.Debug($"Android services initialized, switching to MobileMainWindow @ {DateTime.Now:HH:mm:ss.fff}");

            await UIHelpers.RunOnUIAsync(async () =>
            {
                var main = new MobileMainWindow();
                main.SetDataContext(services.Main);
                main.BrowseContent.DataContext = services.Browse;
                main.InstalledContent.DataContext = services.Installed;
                main.ToolsContent.DataContext = services.Tools;
                main.ExplorerContent.DataContext = services.FileExplorer;

                var ui = new MobileUiActions(services, main);
                ui.Wire();
                Logger.Debug("Android: UI wiring complete");

                rootPanel.Children.Remove(splash);
                rootPanel.Children.Add(main);

                Logger.Debug($"Android: MobileMainWindow loaded @ {DateTime.Now:HH:mm:ss.fff}");

                // Catalog kick, update check, first-run wizard and reconfig prompt
                await ui.RunStartupAsync();
            });
        }
        catch (Exception ex)
        {
            var fullMsg = ex.InnerException != null
                ? $"{ex.Message}\n\nInner: {ex.InnerException.Message}"
                : ex.Message;
            Logger.Error($"Android splash transition failed: {fullMsg}");
            // Still try to show something — replace splash with error
            await UIHelpers.RunOnUIAsync(async () =>
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

    internal static async Task NotifyAutoconnectResultAsync(NotificationCenterService notifications, BackgroundTask task)
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

    internal static async Task CheckForUpdatesAsync(Window? main = null)
    {
        try
        {
            if (!SettingsService.Current.CheckForUpdatesOnStartup)
            {
                Logger.Debug("Check for updates on startup disabled — skipping");
                return;
            }

            using var checker = new GitHubReleaseCheckerService();
            var release = await checker.CheckLatestReleaseAsync();
            if (release is null) return;
            if (!GitHubReleaseCheckerService.IsNewerVersion(release.TagName, BuildInfo.Version))
                return;

            var releaseUrl =
                                      release.HtmlUrl ?? AppUrls.GitHubReleases;
            Logger.Info($"Update available: {release.TagName} (current: {BuildInfo.Version})");

            if (Application.Current?.ApplicationLifetime is IActivityApplicationLifetime)
            {
                _ = UIHelpers.RunOnUIAsync(() =>
                {
                    var mobileDlg = new MobileErrorDialogView
                    {
                        DataContext = new MobileErrorDialogViewModel
                        {
                            Title = "Update Available",
                            Description = $"XB Homebrew Vault {release.TagName} is available. You are currently running {BuildInfo.DisplayVersion}.",
                            Details = "Visit the releases page to download the latest version.",
                            DialogType = ErrorDialogType.Info,
                            DownloadUrl = releaseUrl
                        }
                    };
                    mobileDlg.OkClicked += (_, _) =>
                    {
                        if (TopLevel.GetTopLevel(mobileDlg)?.Content is Panel panel)
                            panel.Children.Remove(mobileDlg);
                    };
                    if (TopLevel.GetTopLevel(mobileDlg)?.Content is Panel targetPanel)
                        targetPanel.Children.Add(mobileDlg);
                    return Task.CompletedTask;
                });
                return;
            }

            var dlg = new ErrorDialog(
                "Update Available",
                $"XB Homebrew Vault {release.TagName} is available. You are currently running {BuildInfo.DisplayVersion}.",
                "Visit the releases page to download the latest version.",
                ErrorDialogType.Info);
            dlg.DownloadAction = () =>
            {
                PlatformHelper.OpenUrl(releaseUrl);
                dlg.Close();
                return Task.CompletedTask;
            };
            if (main is not null)
                await dlg.ShowDialog(main);
            else
                dlg.Show();
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
        Logger.Debug("Rendering: Skia via ANGLE (D3D11), MaxGpuResourceSizeBytes=512MB, UseRegionDirtyRectClipping=true");
    }
}
