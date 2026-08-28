#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XBVault.Helpers;
using XBVault.ViewModels;
using XBVault.Views;

namespace XBVault.Services;

/// <summary>
/// Android-only UI wiring extracted from App.axaml.cs. Receives the shared
/// <see cref="AppServices"/> composition root and the live MobileMainWindow,
/// and assigns every view-model action delegate (overlays, dialogs, SAF file
/// handling, wizards). Overlay hosts (rootPanel/splash) stay in App.
/// </summary>
public sealed class MobileUiActions
{
    private readonly AppServices _s;
    private readonly MobileMainWindow _main;
    private bool _detailOpen;
    private int _detailOverlayDepth;

    private static readonly string[] ScreenshotPatterns = ["*.png"];
    private static readonly string[] PackagePatterns = ["*.appx", "*.msix", "*.appxbundle", "*.msixbundle", "*.zip"];

    public MobileUiActions(AppServices services, MobileMainWindow main)
    {
        _s = services;
        _main = main;
    }

    private XboxAuthService Auth => _s.Auth;
    private IXboxPackageService Package => _s.Package;
    private XboxNetworkService Network => _s.Network;

    /// <summary>Assigns every mobile view-model delegate (runs on the UI thread).</summary>
    public void Wire()
    {
        // ── Virtual keyboard: keep the focused text field visible. The Android
        //    window often doesn't resize under the IME (edge-to-edge), so scroll
        //    the field into view and nudge it above the keyboard area manually.
        _main.AddHandler(InputElement.GotFocusEvent, OnTextFocusChanged, RoutingStrategies.Bubble, handledEventsToo: true);

        // ── Mobile tools: wire action delegates ──
        WireTools();

        _main.SettingsViewModel = _s.Settings;
        _main.NotificationCenter = _s.Notifications;
        _main.BackgroundTasks = _s.BackgroundTasks;
        _main.AuthService = Auth;

        WireShell();
        WireSettingsActions();
        WireBrowseDetail();
        WireInstalledActions();
        WireFileExplorerActions();
    }

    private static void OnTextFocusChanged(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not TextBox tb)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (!tb.IsVisible || tb.Parent is null)
                    return;
                tb.BringIntoView();

                // If the field still sits in the lower half of its scroll area,
                // nudge it up above the likely keyboard zone (~30% from the top).
                var sv = FindAncestorScrollViewer(tb);
                if (sv is null || sv.Viewport.Height <= 0)
                    return;
                var posInViewport = tb.TranslatePoint(new Point(0, 0), sv);
                if (posInViewport is { } pos && pos.Y > sv.Viewport.Height * 0.5)
                {
                    var targetTop = Math.Max(0, pos.Y - sv.Viewport.Height * 0.3);
                    sv.Offset = new Vector(sv.Offset.X, targetTop);
                    Logger.Debug($"Android: nudged focused field above keyboard (offset Y={sv.Offset.Y:F0})");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Android: focus BringIntoView failed: {ex.Message}");
            }
        }, DispatcherPriority.Loaded);
    }

    private static ScrollViewer? FindAncestorScrollViewer(Control control)
    {
        Control? current = control;
        while (current is not null)
        {
            if (current is ScrollViewer sv)
                return sv;
            current = current.Parent as Control;
        }
        return null;
    }

    /// <summary>Catalog kick, update check, first-run wizard and reconfig prompt (runs after the overlay swap).</summary>
    public async Task RunStartupAsync()
    {
        _ = _s.Browse.LoadCatalogCommand.ExecuteAsync(null);
        _ = App.CheckForUpdatesAsync();

        if (!SettingsService.Current.WizardCompleted && !SettingsService.Current.XboxConnection.IsConfigured)
        {
            Logger.Info("Android: Settings not configured, showing setup wizard");
            var wizardVm = new SetupWizardViewModel(Auth);
            var wizardView = new MobileSetupWizardView();
            wizardView.SetViewModel(wizardVm);
            wizardVm.CloseAction = () => _main.CloseOverlay();
            wizardView.CloseRequested += async (_, _) =>
            {
                _main.CloseOverlay();
                if (wizardVm.WasCompleted)
                {
                    Logger.Info("Android: Setup wizard completed");
                    if (wizardVm.OpenConnectionAfter && _s.Main.ShowConnectAction is not null)
                    {
                        var connected = await _s.Main.ShowConnectAction();
                        if (connected)
                        {
                            await Auth.FetchSmbPasswordAsync();
                            _s.Main.IsXboxConnected = true;
                            Auth.MarkConnected();
                            _s.Main.ConnectionStatusText = "Connected";
                        }
                    }
                    _s.Main.UpdateConnectionStatus();
                }
            };
            _main.ShowOverlay(wizardView);
        }

        if (!string.IsNullOrEmpty(SettingsService.Current.XboxConnection.EncryptedPassword)
            && !CryptoService.TryDeobfuscate(SettingsService.Current.XboxConnection.EncryptedPassword, out _))
        {
            Logger.Info("Android: stored password cannot be decrypted — prompting for reconfiguration");
            var pvm = new MobileConfirmDialogViewModel
            {
                Title = "Stored password unavailable",
                Message = "The saved Xbox password could not be decrypted — the configuration file came from another machine/user or is corrupt. Would you like to run the Setup Wizard again to reconfigure?",
                ConfirmText = "Reconfigure",
                CancelText = "Not now"
            };
            var pdlg = new MobileConfirmDialogView { DataContext = pvm };
            var ptcs = pvm.WaitForResult();
            _main.ShowOverlay(pdlg);
            pdlg.SetOnBack(() => { pvm.CancelCommand.Execute(null); _main.CloseOverlay(); });
            var confirmed = await ptcs;
            _main.CloseOverlay();
            if (confirmed && _s.Settings.ReconfigureCredentialsRequested is not null)
                _ = _s.Settings.ReconfigureCredentialsRequested();
        }
    }

    private void WireTools()
    {
        _s.Tools.ShowConnectAction = ShowConnectDialogAsync;
        _s.Tools.ShowScreenshotAction = ShowScreenshot;
        _s.Tools.ShowSystemInfoAction = ShowMobileSystemInfo;
        _s.Tools.ShowCrashDataAction = () => ShowToolOverlay("Crash Data", () => _s.System.GetCrashDumpsAsync());
        _s.Tools.ShowProcessesAction = () => ShowToolOverlay("Processes", () => _s.Process.GetProcessesAsync());
        _s.Tools.ShowNetworkInfoAction = () => ShowToolOverlay("Network Info", () => Network.GetNetworkConfigAsync());
        _s.Tools.ShowPerformanceAction = ShowPerformance;
        _s.Tools.ShowUsbPermissionAction = async () =>
        {
            if (_s.Tools.ShowInfoAsync is not null)
            {
                await _s.Tools.ShowInfoAsync(
                    "Windows Only",
                    "USB Media Drive activation is currently only available on Windows.",
                    "We're evaluating support for Android. Stay tuned for future updates!");
            }
        };
        _s.Tools.OpenLoopbackExemptAction = () => OpenLoopbackExempt(quickMode: false);
        _s.Tools.OpenLoopbackExemptQuickAction = () => OpenLoopbackExempt(quickMode: true);

        _s.Tools.ShowCustomInstallAction = () => ShowMobileCustomInstall().FireAndForget("App.ShowMobileCustomInstall");
        _s.Browse.ShowCustomInstallAction = () => ShowMobileCustomInstall().FireAndForget("App.ShowMobileCustomInstall");
        _s.Browse.OpenUrlAction = url => PlatformHelper.OpenUrl(url);
        _s.Tools.OpenUrlAction = url => PlatformHelper.OpenUrl(url);

        _s.Tools.ShowConfirmAsync = ShowConfirmDialogAsync;
        _s.Tools.ShowInfoAsync = ShowInfoDialogAsync;
    }

    /// <summary>Opens a text-based tool overlay and fetches data on a background thread.</summary>
    private void ShowToolOverlay(string title, Func<Task<string?>> loadData)
    {
        var vm = new MobileToolResultViewModel { IsLoading = true };
        var view = new MobileToolResultView { DataContext = vm };
        var overlay = new MobileToolOverlayView();
        overlay.SetTitle(title);
        overlay.SetContent(view);
        overlay.SetOnBack(() => _main.CloseOverlay());
        _main.ShowOverlay(overlay);
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await loadData();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.ContentText = text ?? "(no data)";
                    vm.IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.StatusMessage = $"Failed to load: {ex.Message}";
                    vm.IsLoading = false;
                });
            }
        });
    }

    private void ShowScreenshot()
    {
        var vm = new MobileScreenshotViewModel(_s.System);
        vm.SaveScreenshotDialog = async stream =>
        {
            var result = new TaskCompletionSource<IStorageFile?>();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var topLevel = TopLevel.GetTopLevel(_main)!;
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Screenshot",
                    SuggestedFileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                    FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("PNG") { Patterns = ScreenshotPatterns } }
                });
                result.SetResult(file);
            });
            var safFile = await result.Task;
            if (safFile is not null)
            {
                await using var fs = await safFile.OpenWriteAsync();
                stream.Position = 0;
                await stream.CopyToAsync(fs);
            }
            return safFile?.Name;
        };
        var sView = new MobileScreenshotView { DataContext = vm };
        var overlay = new MobileToolOverlayView();
        overlay.SetTitle("Screenshot");
        overlay.SetContent(sView);
        overlay.SetOnBack(() => { vm.Dispose(); _main.CloseOverlay(); });
        _main.ShowOverlay(overlay);
    }

    private void ShowMobileSystemInfo()
    {
        var vm = new SystemInfoViewModel(Auth, _s.System);
        var view = new MobileSystemInfoView { DataContext = vm };
        var overlay = new MobileToolOverlayView();
        overlay.SetTitle("System Info");
        overlay.SetContent(view);
        overlay.SetOnBack(() => _main.CloseOverlay());
        _main.ShowOverlay(overlay);
        vm.Initialize();
    }

    private async void ShowPerformance()
    {
        var vm = new MobileToolResultViewModel { IsLoading = true };
        var view = new MobileToolResultView { DataContext = vm };
        var overlay = new MobileToolOverlayView();
        overlay.SetTitle("Performance");
        overlay.SetContent(view);
        overlay.SetOnBack(() => _main.CloseOverlay());
        _main.ShowOverlay(overlay);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _s.Performance.ConnectPerformanceWsAsync(
                snap =>
                {
                    var ica = System.Globalization.CultureInfo.InvariantCulture;
                    var b = new System.Text.StringBuilder();
                    b.AppendLine(ica, $"CPU: {snap.CpuLoad}%");
                    b.AppendLine(ica, $"GPU: {snap.GpuUsage}%");
                    b.AppendLine(ica, $"Memory Used: {snap.MemoryUsedMB:F1} MB");
                    b.AppendLine(ica, $"Memory Total: {snap.MemoryTotalMB:F1} MB");
                    b.AppendLine(ica, $"Committed: {snap.MemoryCommittedBytes / 1024 / 1024} MB");
                    b.AppendLine(ica, $"IO Read: {snap.IOReadSpeed / 1024} KB/s  Write: {snap.IOWriteSpeed / 1024} KB/s");
                    b.AppendLine(ica, $"Net In: {snap.NetworkInBytes / 1024} KB  Out: {snap.NetworkOutBytes / 1024} KB");
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        vm.ContentText = b.ToString();
                        vm.IsLoading = false;
                    });
                }, cts.Token);

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
    }

    private void OpenLoopbackExempt(bool quickMode)
    {
        var vm = new LoopbackExemptViewModel(Auth, _s.Sftp, Package, quickMode: quickMode);
        vm.ShowConfirmAsync = _s.Tools.ShowConfirmAsync;
        vm.CloseAction = () => _main.CloseOverlay();
        vm.OpenProjectLinkAction = () =>
            Process.Start(new ProcessStartInfo(LoopbackExemptViewModel.XFilesProjectUrl) { UseShellExecute = true });
        var lView = new MobileLoopbackView();
        lView.SetViewModel(vm);
        var overlay = new MobileToolOverlayView();
        overlay.SetTitle(quickMode ? "X-Files Enablement" : "Loopback Exempt");
        overlay.SetContent(lView);
        overlay.SetOnBack(() => _main.CloseOverlay());
        _main.ShowOverlay(overlay);
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    private async Task<bool> ShowConnectDialogAsync()
    {
        Logger.Info("Android: ShowConnectAction invoked — opening MobileConnectionView");
        var tcs = new TaskCompletionSource<bool>();
        var connVm = new ConnectionViewModel(Auth, Network);
        var connView = new MobileConnectionView { DataContext = connVm };
        connVm.Completed += success =>
        {
            Logger.Info($"Android: Connection completed: success={success}");
            tcs.SetResult(success);
        };
        connView.SetOnBack(() =>
        {
            if (connVm.IsRunning)
                connVm.CancelCommand.Execute(null);
            _main.CloseOverlay();
            if (!tcs.Task.IsCompleted)
                tcs.SetResult(false);
        });
        _main.ShowOverlay(connView);
        _ = connVm.ConnectCommand.ExecuteAsync(null);
        return await tcs.Task;
    }

    private void WireShell()
    {
        _s.Main.ShowConnectAction = ShowConnectDialogAsync;
        _s.Main.ShowAboutAction = () =>
        {
            Logger.Info("Android: ShowAboutAction invoked");
        };
    }

    private void WireSettingsActions()
    {
        _s.Settings.ShowLogsAction = () =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var vm = new LogsViewModel();
                var logsView = new MobileLogsView { DataContext = vm };
                logsView.SetOnBack(() => _main.CloseOverlay());
                _main.ShowOverlay(logsView);
            });
        };

        _s.Settings.ReconfigureCredentialsRequested = () =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var wizardVm = new SetupWizardViewModel(Auth);
                var wizardView = new MobileSetupWizardView();
                wizardView.SetViewModel(wizardVm);
                wizardVm.CloseAction = () => _main.CloseOverlay();
                wizardView.CloseRequested += async (_, _) =>
                {
                    _main.CloseOverlay();
                    if (wizardVm.WasCompleted)
                        _s.Settings.RefreshFromStorage();
                };
                _main.ShowOverlay(wizardView);
            });
            return Task.CompletedTask;
        };
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string message, string? confirmText, string? cancelText, string? iconSource, string? messageIconSource)
    {
        var vm = new MobileConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText ?? "OK",
            CancelText = cancelText ?? "Cancel",
            ImageSource = iconSource
        };
        var dlg = new MobileConfirmDialogView { DataContext = vm };
        var tcs = vm.WaitForResult();
        _main.ShowOverlay(dlg);
        dlg.SetOnBack(() => { vm.CancelCommand.Execute(null); _main.CloseOverlay(); });
        var result = await tcs;
        _main.CloseOverlay();
        return result;
    }

    private async Task ShowInfoDialogAsync(string? title, string? description, string? details)
    {
        var vm = new MobileInfoDialogViewModel
        {
            Title = title ?? "",
            Description = description ?? "",
            Details = details
        };
        var dlg = new MobileInfoDialogView { DataContext = vm };
        var tcs = vm.WaitForResult();
        _main.ShowOverlay(dlg);
        dlg.SetOnBack(() => { vm.OkCommand.Execute(null); _main.CloseOverlay(); });
        await tcs;
        _main.CloseOverlay();
    }

    private void WireBrowseDetail()
    {
        _s.Browse.ShowDetailAction = item =>
        {
            if (_detailOpen)
            {
                Logger.Debug($"Android: ShowDetailAction ignored — a detail is already open ({item.Name})");
                return;
            }
            _detailOpen = true;
            _detailOverlayDepth = _main.OverlayDepth;
            Logger.Info($"Android: ShowDetailAction for {item.Name}");
            var detail = new MobileDetailView { DataContext = _s.Browse };
            detail.SetOnBack(() =>
            {
                _detailOpen = false;
                _main.CloseOverlay();
                if (_s.Browse.IsUpdateComplete)
                    _ = _s.Installed.RefreshPackagesCommand.ExecuteAsync(null);
                _s.Browse.IsUpdateMode = false;
                _s.Browse.SelectedItem = null;
            });
            _s.Browse.CloseDetailAction = () => CloseDetailOverlays();
            _main.ShowOverlay(detail);
        };

        _s.Main.OnTabChanged = tab =>
                {
                    try
                    {
                        Logger.Info($"Android: OnTabChanged → tab {tab}");
                        if (tab == 1)
                        {
                            if (_s.Installed.IsConnected != Auth.IsConnected)
                            {
                                Logger.Debug($"Android: InstalledView IsConnected desync fix — was {_s.Installed.IsConnected}, correcting to {Auth.IsConnected}");
                                _s.Installed.IsConnected = Auth.IsConnected;
                            }
                            _s.Installed.StartPolling();
                            if (Auth.IsConnected)
                                _ = _s.Installed.RefreshPackagesCommand.ExecuteAsync(null);
                        }
                        else
                        {
                            _s.Installed.StopPolling();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Android: OnTabChanged handler failed for tab {tab}");
                    }
                };
    }

    private void WireInstalledActions()
    {
        _s.Installed.ShowConnectAction = async () =>
        {
            Logger.Info("Android: InstalledView ShowConnectAction — opening MobileConnectionView");
            var tcs = new TaskCompletionSource<bool>();
            var connVm = new ConnectionViewModel(Auth, Network);
            var connView = new MobileConnectionView { DataContext = connVm };
            connVm.Completed += success =>
            {
                Logger.Info($"Android: Connection completed: success={success}");
                tcs.SetResult(success);
            };
            connView.SetOnBack(() =>
            {
                if (connVm.IsRunning)
                    connVm.CancelCommand.Execute(null);
                _main.CloseOverlay();
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(false);
            });
            _main.ShowOverlay(connView);
            _ = connVm.ConnectCommand.ExecuteAsync(null);
            return await tcs.Task;
        };

        _s.Installed.ShowErrorAction = ShowInfoDialogAsync;
        _s.Installed.ShowErrorWithConnectAction = async (title, description, details, connectAction) =>
        {
            await ShowInfoDialogAsync(title, description, details);
            if (connectAction is not null)
                await connectAction();
        };

        _s.Installed.ConfirmAutostartAction = async (pkg, previousName) =>
        {
            var message = string.IsNullOrEmpty(previousName)
                ? $"Launch {pkg.Name} automatically when XBVault connects to the Xbox?"
                : $"Replace {previousName} with {pkg.Name} as the app that launches automatically on connect?";
            return await ShowConfirmDialogAsync(
                            "Autostart on Connect", message, "Enable", "Cancel",
                            "avares://XBVault/Assets/Views/InstalledView/installed-autostart-100.png", null);
        };

        _s.Installed.ConfirmUninstallAsync = async pkg =>
            await ShowConfirmDialogAsync(
                "Uninstall Package", $"Are you sure you want to uninstall {pkg.Name}?", "Uninstall", "Cancel",
                "avares://XBVault/Assets/Views/InstalledView/installed-uninstall-100.png", null);

        _s.Installed.NotifyAutostartAction = message =>
        {
            _s.Notifications.Notify(
                "Autostart",
                message,
                "avares://XBVault/Assets/Views/InstalledView/installed-autostart-16.png");
        };

        _s.Installed.ResolveBannerAsync = pkg => _s.Browse.FindThumbnailByPackageAsync(pkg);
        _s.Installed.CheckOutdatedAsync = async pkg =>
        {
            var result = _s.Browse.FindCatalogMatch(pkg);
            return result;
        };

        _s.Installed.ShowCatalogDetailAction = catalogItem =>
        {
            _s.Browse.IsUpdateMode = true;
            _s.Browse.SelectedItem = catalogItem;
            OpenBrowseDetail();
        };
        _s.Installed.ShowInstalledDetailAction = (pkg, match, isOutdated) =>
        {
            _s.Browse.SelectedInstalledPackage = pkg;
            _s.Browse.IsInstalledMode = !isOutdated;
            _s.Browse.IsUpdateMode = isOutdated;
            _s.Browse.SelectedItem = match;
            OpenBrowseDetail();
        };

        _s.Installed.ReinstallInstallAction = () =>
        {
            _s.Browse.IsInstalledMode = false;
            _s.Browse.SelectedInstalledPackage = null;
            _s.Browse.IsUpdateMode = false;
            _s.Browse.InstallSelectedCommand.Execute(null);
        };
        _s.Installed.ConfirmReinstallAsync = async pkg =>
                    await ShowConfirmDialogAsync(
                        "Confirm Reinstall", $"Reinstall \"{pkg.Name}\"? This will reinstall over the existing installation and keep your app data.", "Reinstall", "Cancel",
                        "avares://XBVault/Assets/Views/InstalledView/installed-update-100.png", null);

        _s.Browse.UninstallFromDetailAction = pkg =>
                {
                    _s.Browse.CloseDetailAction?.Invoke();
                    _ = _s.Installed.UninstallPackageCommand.ExecuteAsync(pkg);
                };
        // Uninstall is handled by the Installed tab: surface its "uninstalling"
        // progress by switching the main window there as soon as it starts.
        _s.Installed.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_s.Installed.IsUninstalling) && _s.Installed.IsUninstalling)
                _main.SwitchToInstalledTab();
        };
        _s.Browse.ReinstallFromDetailAction = pkg =>
            _s.Installed.ReinstallPackageCommand.ExecuteAsync(pkg);

        _s.Installed.ShowCustomInstallAction = () => ShowMobileCustomInstall().FireAndForget("App.ShowMobileCustomInstall");
        _s.Installed.OpenCustomInstallWithFileAction = filePath =>
        {
            ShowMobileCustomInstallWithFile(filePath);
            return Task.CompletedTask;
        };
    }

    private void OpenBrowseDetail()
    {
        if (_detailOpen)
        {
            Logger.Debug("Android: OpenBrowseDetail ignored — a detail is already open");
            return;
        }
        _detailOpen = true;
        _detailOverlayDepth = _main.OverlayDepth;
        var detail = new MobileDetailView { DataContext = _s.Browse };
        detail.SetOnBack(() =>
        {
            _detailOpen = false;
            _main.CloseOverlay();
            if (_s.Browse.IsUpdateComplete)
                _ = _s.Installed.RefreshPackagesCommand.ExecuteAsync(null);
            _s.Browse.IsUpdateMode = false;
            _s.Browse.IsInstalledMode = false;
            _s.Browse.SelectedInstalledPackage = null;
            _s.Browse.SelectedItem = null;
        });
        _s.Browse.CloseDetailAction = CloseDetailOverlays;
        _main.ShowOverlay(detail);
    }

    // Closes every overlay stacked at or above the depth the detail was opened
    // at. A single CloseOverlay() only pops the top layer, so a detail opened on
    // top of another leftover (double-tap on the card) would otherwise survive
    // the uninstall/close and look like a modal that never closes.
    private void CloseDetailOverlays()
    {
        while (_main.OverlayDepth > _detailOverlayDepth)
            _main.CloseOverlay();
        _detailOpen = false;
    }

    private void WireFileExplorerActions()
    {
        _s.FileExplorer.ShowDeleteConfirmAsync = async entries =>
        {
            var hasFolders = entries.Any(e => e.IsDirectory);
            var suffix = hasFolders ? " (including all contents)" : "";
            var summary = entries.Count == 1
                ? $"Delete {entries[0].Name}{suffix}?"
                : $"Delete {entries.Count} items{suffix}?";

            return await ShowConfirmDialogAsync(
                "Confirm Delete", summary, "Delete", "Cancel",
                "avares://XBVault/Assets/Views/MobileFileExplorer/mobilefileexplorer-delete-32.png", null);
        };

        _s.FileExplorer.ShowConnectionInfoAsync = async (host, user, password, port) =>
        {
            var vm = new MobileSftpInfoViewModel
            {
                Host = host,
                User = user,
                Password = password,
                Port = port
            };
            var view = new MobileSftpInfoView { DataContext = vm };
            view.BackRequested += (_, _) => _main.CloseOverlay();
            _main.ShowOverlay(view);
            await Task.CompletedTask;
        };

        _s.FileExplorer.ShowConnectAction = async () =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var connVm = new ConnectionViewModel(Auth, Network);
            var connView = new MobileConnectionView { DataContext = connVm };
            connVm.Completed += success => tcs.SetResult(success);
            connView.SetOnBack(() =>
            {
                if (connVm.IsRunning) connVm.CancelCommand.Execute(null);
                _main.CloseOverlay();
                if (!tcs.Task.IsCompleted) tcs.SetResult(false);
            });
            _main.ShowOverlay(connView);
            _ = connVm.ConnectCommand.ExecuteAsync(null);
            return await tcs.Task;
        };

        _s.FileExplorer.ShowErrorDialog = (title, description, details) =>
        {
            var vm = new MobileInfoDialogViewModel
            {
                Title = title,
                Description = description ?? "",
                Details = details
            };
            var dlg = new MobileInfoDialogView { DataContext = vm };
            var tcs = vm.WaitForResult();
            dlg.SetOnBack(() => { vm.OkCommand.Execute(null); _main.CloseOverlay(); });
            _main.ShowOverlay(dlg);
            _ = tcs.ContinueWith(_ => Dispatcher.UIThread.Post(() => _main.CloseOverlay()));
        };

        _s.FileExplorer.ShowInputDialogAsync = async (title, message, defaultValue, iconUri) =>
        {
            var vm = new MobileInputDialogViewModel
            {
                Title = title,
                Message = message,
                Value = defaultValue
            };
            var dlg = new MobileInputDialogView { DataContext = vm };
            var tcs = vm.WaitForResult();
            dlg.BackRequested += (_, _) => { vm.CancelCommand.Execute(null); _main.CloseOverlay(); };
            _main.ShowOverlay(dlg);
            var result = await tcs;
            _main.CloseOverlay();
            return result;
        };

        _s.FileExplorer.ShowConfirmAction = async (title, message, confirmText, cancelText) =>
        {
            var vm = new MobileConfirmDialogViewModel
            {
                Title = title,
                Message = message,
                ConfirmText = confirmText ?? "OK",
                CancelText = cancelText ?? "Cancel"
            };
            var dlg = new MobileConfirmDialogView { DataContext = vm };
            var tcs = vm.WaitForResult();
            dlg.SetOnBack(() => { vm.CancelCommand.Execute(null); _main.CloseOverlay(); });
            _main.ShowOverlay(dlg);
            var result = await tcs;
            _main.CloseOverlay();
            return result;
        };

        _s.FileExplorer.ShowSaveFileDialogAsync = async entry =>
        {
            var topLevel = TopLevel.GetTopLevel(_main)!;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = entry.Name
            });
            if (file is null) return null;
            var localPath = file.TryGetLocalPath();
            if (!string.IsNullOrEmpty(localPath))
                return localPath;
            var tempPath = Path.Combine(Path.GetTempPath(), $"xbv_dl_{entry.Name}");
            _s.FileExplorer.PendingSaveFile = file;
            _s.FileExplorer.PendingSaveTempPath = tempPath;
            return tempPath;
        };

        _s.FileExplorer.PostDownloadSaveAsync = async tempPath =>
        {
            if (_s.FileExplorer.PendingSaveFile is { } safFile &&
                !string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    await using var srcStream = File.OpenRead(tempPath);
                    var storageFile = (IStorageFile)safFile;
                    await using var dstStream = await storageFile.OpenWriteAsync();
                    await srcStream.CopyToAsync(dstStream);
                }
                finally
                {
                    try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
                    _s.FileExplorer.PendingSaveFile = null;
                    _s.FileExplorer.PendingSaveTempPath = null;
                }
            }
        };

        _s.FileExplorer.ShowFolderPickerAsync = async () =>
        {
            var topLevel = TopLevel.GetTopLevel(_main)!;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select destination folder",
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        };

        _s.FileExplorer.ScrollToEntry = _ => { };
        _s.FileExplorer.FocusFileList = () => { };

        _s.FileExplorer.OpenCustomInstallWithFileAction = filePath =>
        {
            ShowMobileCustomInstallWithFile(filePath);
            return Task.CompletedTask;
        };
    }

    private async Task ShowMobileCustomInstall()
    {
        if (!Auth.IsConnected)
        {
            Logger.Info("ShowMobileCustomInstall: not connected, opening connection view");
            var tcs = new TaskCompletionSource<bool>();
            var connVm = new ConnectionViewModel(Auth, null!);
            var connView = new MobileConnectionView { DataContext = connVm };
            connVm.Completed += success => tcs.SetResult(success);
            connView.SetOnBack(() =>
            {
                if (connVm.IsRunning) connVm.CancelCommand.Execute(null);
                _main.CloseOverlay();
                if (!tcs.Task.IsCompleted) tcs.SetResult(false);
            });
            _main.ShowOverlay(connView);
            _ = connVm.ConnectCommand.ExecuteAsync(null);
            if (!await tcs.Task)
            {
                Logger.Info("ShowMobileCustomInstall: user cancelled connection");
                return;
            }
        }

        Logger.Debug("ShowMobileCustomInstall: creating view and VM");
        try
        {
            var vm = new CustomInstallViewModel(Package, _s.Install);
            Logger.Debug("ShowMobileCustomInstall: VM created, setting up delegates");
            var pickFileFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Package files")
                {
                    Patterns = PackagePatterns
                }
            };
            vm.PickFileAsync = async () =>
            {
                try
                {
                    var topLevel = TopLevel.GetTopLevel(_main)!;
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select Package",
                        AllowMultiple = false,
                        FileTypeFilter = pickFileFilter
                    });
                    if (files is not { Count: > 0 })
                        return null;

                    var localPath = files[0].TryGetLocalPath();
                    if (!string.IsNullOrEmpty(localPath))
                        return localPath;

                    await using var stream = await files[0].OpenReadAsync();
                    var originalName = files[0].Name ?? "package";
                    var tempPath = Path.Combine(Path.GetTempPath(), originalName);
                    await using (var fs = File.Create(tempPath))
                        await stream.CopyToAsync(fs);
                    Logger.Info($"PickFileAsync: copied SAF file to temp path — {tempPath}");
                    return tempPath;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "PickFileAsync: file picker failed");
                    return null;
                }
            };
            vm.PickDependencyFilesAsync = async () =>
            {
                try
                {
                    var topLevel = TopLevel.GetTopLevel(_main)!;
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select Dependencies",
                        AllowMultiple = true,
                        FileTypeFilter = pickFileFilter
                    });
                    if (files is null || files.Count == 0)
                        return null;

                    var result = new List<string>();
                    foreach (var f in files)
                    {
                        var localPath = f.TryGetLocalPath();
                        if (!string.IsNullOrEmpty(localPath))
                        {
                            result.Add(localPath);
                            continue;
                        }

                        await using var stream = await f.OpenReadAsync();
                        var depName = f.Name ?? "dependency";
                        var tempPath = Path.Combine(Path.GetTempPath(), depName);
                        await using (var fs = File.Create(tempPath))
                            await stream.CopyToAsync(fs);
                        Logger.Info($"PickDependencyFilesAsync: copied SAF file to temp path — {tempPath}");
                        result.Add(tempPath);
                    }
                    return result.ToArray();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "PickDependencyFilesAsync: file picker failed");
                    return null;
                }
            };
            vm.CloseAction = () => { _main.CloseOverlay(); vm.Dispose(); };
            var ciView = new MobileCustomInstallView();
            ciView.SetViewModel(vm);
            ciView.CloseRequested += (_, _) => { _main.CloseOverlay(); vm.Dispose(); };
            Logger.Debug("ShowMobileCustomInstall: calling ShowOverlay");
            _main.ShowOverlay(ciView);
            Logger.Debug("ShowMobileCustomInstall: overlay shown");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ShowMobileCustomInstall failed");
            Dispatcher.UIThread.Post(() =>
                App.ShowErrorDialogSafe("Custom Install Error",
                    "Failed to open the custom install wizard.",
                    ex.ToString(), ErrorDialogType.Error));
        }
    }

    private void ShowMobileCustomInstallWithFile(string filePath)
    {
        try
        {
            var vm = new CustomInstallViewModel(Package, _s.Install);
            vm.PickFileAsync = () => Task.FromResult<string?>(filePath);
            vm.PickDependencyFilesAsync = () => Task.FromResult<string[]?>(null);
            vm.CloseAction = () => { _main.CloseOverlay(); vm.Dispose(); };
            var ciView = new MobileCustomInstallView();
            ciView.SetViewModel(vm);
            ciView.CloseRequested += (_, _) => { _main.CloseOverlay(); vm.Dispose(); };
            _main.ShowOverlay(ciView);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ShowMobileCustomInstallWithFile failed");
            Dispatcher.UIThread.Post(() =>
                App.ShowErrorDialogSafe("Custom Install Error",
                    "Failed to open the custom install wizard.",
                    ex.ToString(), ErrorDialogType.Error));
        }
    }
}
