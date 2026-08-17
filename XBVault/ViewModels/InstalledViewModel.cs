#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

/// <summary>
/// Coordinates installed-package listing, running-state polling, launch/terminate actions, and update checks.
/// </summary>
/// <remarks>
/// The class owns package state for the Installed screen. Platform UI concerns such as confirmation dialogs
/// are exposed through callback delegates so other frontends can reuse the same workflow.
/// </remarks>
public partial class InstalledViewModel : ObservableObject
{
    private readonly IXboxAuthService _authService;
    private readonly IXboxPackageService _packageService;
    private readonly PackageLauncher _launcher;
    private readonly List<InstalledPackage> _allPackages = [];

    public Func<string, Task>? OpenCustomInstallWithFileAction { get; set; }
    public Action? ShowCustomInstallAction { get; set; }
    public Func<Task>? RescanUpdatesAction { get; set; }

    public InstalledViewModel(
        IXboxAuthService authService,
        IXboxPackageService packageService,
        PackageLauncher? launcher = null)
    {
        _authService = authService;
        _packageService = packageService;
        _launcher = launcher ?? new PackageLauncher(packageService);
        _authService.ConnectionChanged += OnConnectionChanged;
        IsConnected = _authService.IsConnected;
        Logger.Debug("InstalledViewModel initialized");
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
        if (connected)
            StatusMessage = null;
        if (connected)
            _ = LaunchAutostartAsync();
    }

    private DispatcherTimer? _pollTimer;

    public void StartPolling()
    {
        if (_pollTimer is not null) return;
        IsPolling = true;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _pollTimer.Tick += async (_, _) =>
        {
            if (_allPackages.Count == 0) return;
            Logger.Debug("Polling running state...");
            await RefreshRunningStateAsync();
            LastUpdated = "Updated: " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        };
        _pollTimer.Start();
        Logger.Info("Running-state polling started (8s interval)");
    }

    public void StopPolling()
    {
        if (_pollTimer is null) return;
        _pollTimer.Stop();
        _pollTimer = null;
        IsPolling = false;
        Logger.Info("Running-state polling stopped");
    }

    public ObservableCollection<InstalledPackage> Packages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    [NotifyPropertyChangedFor(nameof(IsPackageSelected))]
    [NotifyPropertyChangedFor(nameof(IsPackageRunning))]
    [NotifyPropertyChangedFor(nameof(IsPackageNotRunning))]
    [NotifyPropertyChangedFor(nameof(IsPackageSelectedNotRunning))]
    [NotifyPropertyChangedFor(nameof(CanToggleAutostart))]
    [NotifyPropertyChangedFor(nameof(CanUpdateSelected))]
    [NotifyPropertyChangedFor(nameof(UpdateTooltipMessage))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isPolling;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    [NotifyPropertyChangedFor(nameof(IsPackageSelected))]
    [NotifyPropertyChangedFor(nameof(IsPackageRunning))]
    [NotifyPropertyChangedFor(nameof(IsPackageNotRunning))]
    [NotifyPropertyChangedFor(nameof(IsPackageSelectedNotRunning))]
    [NotifyPropertyChangedFor(nameof(CanToggleAutostart))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(CanUpdateSelected))]
    [NotifyPropertyChangedFor(nameof(UpdateTooltipMessage))]
    private bool _isUninstalling;

    public Cursor? Cursor => (IsLoading || IsPolling) ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    [ObservableProperty]
    private string? _lastUpdated;

    [ObservableProperty]
    private bool _hasPackages;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _toolbarStatus;

    [ObservableProperty]
    private string? _searchText;

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Packages.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allPackages
            : _allPackages.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (p.DisplayName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Publisher?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        var result = filtered.ToList();
        foreach (var pkg in result)
            Packages.Add(pkg);
        HasPackages = Packages.Count > 0;
    }

    [ObservableProperty]
    private bool _isConnected;

    public bool ShowDisconnected => !IsConnected && !IsLoading;
    public bool ShowStatus => IsConnected && !string.IsNullOrEmpty(StatusMessage);
    public bool ShowGrid => HasPackages && !IsLoading && IsConnected;
    public bool ShowRefreshPrompt => !IsLoading && !HasPackages && string.IsNullOrEmpty(StatusMessage) && IsConnected;

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowRefreshPrompt));
        OnPropertyChanged(nameof(ShowStatus));
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisconnected));
        OnPropertyChanged(nameof(ShowStatus));
        OnPropertyChanged(nameof(ShowRefreshPrompt));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(IsPackageSelected));
        OnPropertyChanged(nameof(IsPackageRunning));
        OnPropertyChanged(nameof(IsPackageNotRunning));
        OnPropertyChanged(nameof(IsPackageSelectedNotRunning));
        OnPropertyChanged(nameof(IsSelectedPackageAutostart));
        OnPropertyChanged(nameof(CanToggleAutostart));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisconnected));
        OnPropertyChanged(nameof(ShowRefreshPrompt));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(IsPackageSelected));
        OnPropertyChanged(nameof(IsPackageRunning));
        OnPropertyChanged(nameof(IsPackageNotRunning));
        OnPropertyChanged(nameof(IsPackageSelectedNotRunning));
    }

    partial void OnHasPackagesChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRefreshPrompt));
        OnPropertyChanged(nameof(ShowGrid));
    }

    [ObservableProperty]
    private InstalledPackage? _selectedPackage;

    public bool IsPackageSelected => SelectedPackage is not null && !IsLoading && !IsUninstalling && IsConnected;
    public bool IsPackageRunning => !IsLoading && !IsUninstalling && (SelectedPackage?.IsRunning ?? false) && IsConnected;
    public bool IsPackageNotRunning => !IsLoading && !IsUninstalling && (SelectedPackage is null || !SelectedPackage.IsRunning);
    public bool IsPackageSelectedNotRunning => !IsLoading && !IsUninstalling && SelectedPackage is not null && !SelectedPackage.IsRunning && IsConnected;
    public bool IsSelectedPackageAutostart => SelectedPackage?.IsAutostart ?? false;
    public bool CanToggleAutostart => IsPackageSelected && !IsSelectedPackageAutostart;
    public bool CanRefresh => !IsUninstalling && IsConnected;
    public bool CanUpdateSelected => IsPackageSelected && (SelectedPackage?.IsOutdated ?? false);
    public string UpdateTooltipMessage => SelectedPackage is null ? "Select a package" : SelectedPackage.IsOutdated ? "Update available" : "No update available";

    private InstalledPackage? _prevSelectedPackage;

    partial void OnSelectedPackageChanged(InstalledPackage? value)
    {
        if (_prevSelectedPackage is not null)
        {
            _prevSelectedPackage.IsSelected = false;
            _prevSelectedPackage.PropertyChanged -= OnSelectedPackagePropertyChanged;
        }
        _prevSelectedPackage = value;

        if (value is not null)
        {
            value.IsSelected = true;
            value.PropertyChanged += OnSelectedPackagePropertyChanged;
        }

        OnPropertyChanged(nameof(IsPackageSelected));
        OnPropertyChanged(nameof(IsPackageRunning));
        OnPropertyChanged(nameof(IsPackageNotRunning));
        OnPropertyChanged(nameof(IsPackageSelectedNotRunning));
        OnPropertyChanged(nameof(IsSelectedPackageAutostart));
        OnPropertyChanged(nameof(CanToggleAutostart));
        NotifyUpdateState();
        if (value is not null)
        {
            Logger.Info($"Selected package raw:\n{value.RawJson}");
        }
    }

    private void OnSelectedPackagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledPackage.IsOutdated))
            NotifyUpdateState();
        if (e.PropertyName == nameof(InstalledPackage.IsAutostart))
        {
            OnPropertyChanged(nameof(IsSelectedPackageAutostart));
            OnPropertyChanged(nameof(CanToggleAutostart));
        }
    }

    private void NotifyUpdateState()
    {
        OnPropertyChanged(nameof(CanUpdateSelected));
        OnPropertyChanged(nameof(UpdateTooltipMessage));
    }

    private void UpdateRunningState()
    {
        OnPropertyChanged(nameof(IsPackageRunning));
        OnPropertyChanged(nameof(IsPackageNotRunning));
        OnPropertyChanged(nameof(IsPackageSelectedNotRunning));
    }

    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Func<string, string, string, Task>? ShowErrorAction { get; set; }
    public Func<string, string, string, Func<Task>?, Task>? ShowErrorWithConnectAction { get; set; }
    public Func<InstalledPackage, Task<Bitmap?>>? ResolveBannerAsync { get; set; }
    public Func<InstalledPackage, Task<(CatalogItem? match, bool isOutdated)>>? CheckOutdatedAsync { get; set; }
    public Action<CatalogItem>? ShowCatalogDetailAction { get; set; }
    public Action? OnCatalogReady { get; set; }
    public Func<InstalledPackage, string?, Task<bool>>? ConfirmAutostartAction { get; set; }
    public Action<string>? NotifyAutostartAction { get; set; }

    [ObservableProperty]
    private bool _isCatalogReady;

    private static Bitmap? _genericBanner;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (ShowConnectAction is not null)
        {
            var ok = await ShowConnectAction();
            if (ok)
                _authService.MarkConnected();
        }
    }

    [RelayCommand]
    private void OpenCustomInstall() => ShowCustomInstallAction?.Invoke();

    [RelayCommand]
    private async Task UpdateSelectedAsync()
    {
        if (SelectedPackage is null || CheckOutdatedAsync is null || ShowCatalogDetailAction is null)
            return;

        try
        {
            var (match, _) = await CheckOutdatedAsync(SelectedPackage);
            if (match is not null)
                ShowCatalogDetailAction(match);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"UpdateSelected failed for {SelectedPackage.Name}");
        }
    }

    private static bool IsAutostartApp(InstalledPackage pkg)
    {
        var fullName = AutostartService.GetAutostartFullName();
        return !string.IsNullOrEmpty(fullName) && pkg.FullName == fullName;
    }

    private void SyncAutostartFlags()
    {
        foreach (var pkg in _allPackages)
            pkg.IsAutostart = IsAutostartApp(pkg);
    }

    private void SyncIgnoreUpdateFlags()
    {
        var ignored = SettingsService.Current?.IgnoredUpdatePackageFamilies ?? [];
        foreach (var pkg in _allPackages)
            pkg.IgnoreUpdateAlerts = !string.IsNullOrEmpty(pkg.PackageFamilyName) &&
                ignored.Contains(pkg.PackageFamilyName, StringComparer.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task ToggleIgnoreUpdateAsync(InstalledPackage pkg)
    {
        if (pkg is null) return;

        pkg.IgnoreUpdateAlerts = !pkg.IgnoreUpdateAlerts;

        var settings = SettingsService.Current;
        var list = settings?.IgnoredUpdatePackageFamilies ?? [];
        if (string.IsNullOrEmpty(pkg.PackageFamilyName))
            return;

        if (pkg.IgnoreUpdateAlerts)
        {
            if (!list.Contains(pkg.PackageFamilyName, StringComparer.OrdinalIgnoreCase))
                list.Add(pkg.PackageFamilyName);
            ToolbarStatus = $"Update alerts ignored: {pkg.Name}";
            Logger.Info($"Ignoring update alerts for {pkg.Name} ({pkg.PackageFamilyName})");
        }
        else
        {
            list.RemoveAll(x => x.Equals(pkg.PackageFamilyName, StringComparison.OrdinalIgnoreCase));
            ToolbarStatus = $"Update alerts enabled: {pkg.Name}";
            Logger.Info($"Re-enabling update alerts for {pkg.Name} ({pkg.PackageFamilyName})");
        }
        SettingsService.Save();

        if (CheckOutdatedAsync is not null)
        {
            try
            {
                var (_, outdated) = await CheckOutdatedAsync(pkg);
                pkg.IsOutdated = outdated;
            }
            catch (Exception ex)
            {
                Logger.Trace($"ToggleIgnoreUpdateAsync: outdated re-check failed for {pkg.Name} — {ex.Message}");
            }
        }

        if (RescanUpdatesAction is not null)
        {
            try { await RescanUpdatesAction(); }
            catch (Exception ex) { Logger.Trace($"ToggleIgnoreUpdateAsync: rescan failed — {ex.Message}"); }
        }
    }

    [RelayCommand]
    private async Task ToggleAutostartAsync(InstalledPackage pkg)
    {
        if (pkg is null) return;

        var current = AutostartService.GetAutostartFullName();
        if (!string.IsNullOrEmpty(current) && current == pkg.FullName)
        {
            AutostartService.ClearAutostart();
            pkg.IsAutostart = false;
            ToolbarStatus = $"Autostart removed: {pkg.Name}";
            return;
        }

        var previousPkg = _allPackages.FirstOrDefault(p => p.FullName == current);
        if (ConfirmAutostartAction is not null)
        {
            var ok = await ConfirmAutostartAction(pkg, previousPkg?.Name);
            if (!ok) return;
        }

        AutostartService.SetAutostart(pkg.FullName);
        foreach (var p in _allPackages)
            p.IsAutostart = p.FullName == pkg.FullName;
        ToolbarStatus = $"Autostart on connect: {pkg.Name}";
        Logger.Info($"Autostart enabled for {pkg.Name}");
    }

    private async Task LaunchAutostartAsync()
    {
        var fullName = AutostartService.GetAutostartFullName();
        if (string.IsNullOrEmpty(fullName))
            return;

        var candidates = _allPackages;
        if (candidates.Count == 0)
        {
            Logger.Debug("LaunchAutostartAsync: no installed list yet — fetching");
            try
            {
                var packages = await _packageService.GetInstalledPackagesAsync();
                candidates = packages;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "LaunchAutostartAsync: failed to fetch installed packages");
                return;
            }
        }

        var pkg = candidates.FirstOrDefault(p => p.FullName == fullName);
        if (pkg is null)
        {
            AutostartService.ClearAutostart();
            SyncAutostartFlags();
            Logger.Warn($"Autostart app {fullName} no longer installed — selection cleared");
            NotifyAutostartAction?.Invoke($"Autostart app no longer installed — cleared. Re-enable from the Installed tab.");
            return;
        }

        if (pkg.IsRunning)
        {
            Logger.Info($"Autostart: {pkg.Name} already running — skipping");
            return;
        }

        Logger.Info($"Autostart: launching {pkg.Name} on connect");
        var result = await _launcher.LaunchAsync(pkg, _allPackages, status => ToolbarStatus = status);
        if (!result.Success)
        {
            Logger.Warn($"Autostart launch failed for {pkg.Name}: {result.Error}");
            NotifyAutostartAction?.Invoke($"Failed to auto-launch {pkg.Name}: {result.Error}");
        }
        else
        {
            ToolbarStatus = $"Launched: {pkg.Name}";
            _ = RefreshRunningStateAsync();
        }
    }

    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        if (SelectedPackage is null || SelectedPackage.IsRunning) return;

        var result = await _launcher.LaunchAsync(SelectedPackage, _allPackages, status => ToolbarStatus = status);
        if (result.Success)
            _ = RefreshRunningStateAsync();
        else if (result.SuspendFailed)
            await RefreshPackagesAsync();
        else
            ToolbarStatus = $"Launch failed: {result.Error}";
    }

    [RelayCommand]
    private async Task SuspendSelectedAsync()
    {
        if (SelectedPackage is null || !SelectedPackage.IsRunning) return;
        var ok = await _packageService.SuspendPackageAsync(SelectedPackage.FullName);
        if (ok)
        {
            SelectedPackage.IsRunning = false;
            ToolbarStatus = $"Suspended: {SelectedPackage.Name}";
            UpdateRunningState();
        }
        else
        {
            ToolbarStatus = $"Suspend failed: {SelectedPackage.Name}";
        }
    }

    [RelayCommand]
    private async Task TerminateSelectedAsync()
    {
        if (SelectedPackage is null || !SelectedPackage.IsRunning) return;
        var ok = await _packageService.TerminatePackageAsync(SelectedPackage.FullName);
        if (ok)
        {
            SelectedPackage.IsRunning = false;
            ToolbarStatus = $"Terminated: {SelectedPackage.Name}";
            UpdateRunningState();
            _ = RefreshRunningStateAsync();
        }
        else
        {
            ToolbarStatus = $"Terminate failed: {SelectedPackage.Name}";
        }
    }

    [RelayCommand]
    private async Task LaunchPackageAsync(InstalledPackage pkg)
    {
        if (pkg is null || pkg.IsRunning) return;

        var result = await _launcher.LaunchAsync(pkg, _allPackages, status => ToolbarStatus = status);
        if (result.Success)
            _ = RefreshRunningStateAsync();
        else if (result.SuspendFailed)
            await RefreshPackagesAsync();
        else
            ToolbarStatus = $"Launch failed: {result.Error}";
    }

    [RelayCommand]
    private async Task SuspendPackageAsync(InstalledPackage pkg)
    {
        if (pkg is null || !pkg.IsRunning) return;

        var ok = await _packageService.SuspendPackageAsync(pkg.FullName);
        if (ok)
        {
            pkg.IsRunning = false;
            ToolbarStatus = $"Suspended: {pkg.Name}";
        }
        else
        {
            ToolbarStatus = $"Suspend failed: {pkg.Name}";
        }
    }

    [RelayCommand]
    private async Task TerminatePackageAsync(InstalledPackage pkg)
    {
        if (pkg is null || !pkg.IsRunning) return;

        var ok = await _packageService.TerminatePackageAsync(pkg.FullName);
        if (ok)
        {
            pkg.IsRunning = false;
            ToolbarStatus = $"Terminated: {pkg.Name}";
        }
        else
        {
            ToolbarStatus = $"Terminate failed: {pkg.Name}";
        }
    }

    private async Task RefreshRunningStateAsync()
    {
        if (!await _authService.EnsureConnectedAsync())
            return;

        var running = await _packageService.GetRunningPackageNamesAsync();

        if (running.Count > 0)
        {
            foreach (var pkg in _allPackages)
                pkg.IsRunning = running.Contains(pkg.FullName) || running.Contains(pkg.PackageFamilyName ?? "");
        }
        // else: keep existing IsRunning (local tracking)
        UpdateRunningState();
    }

    [RelayCommand]
    private async Task RefreshPackagesAsync()
    {
        if (!await _authService.EnsureConnectedAsync())
        {
            Logger.Info("Xbox not connected — showing error dialog");
            if (ShowErrorWithConnectAction is not null)
            {
                Func<Task> connectAndRetry = async () =>
                {
                    if (ShowConnectAction is not null)
                    {
                        var ok = await ShowConnectAction();
                        if (ok) await RefreshPackagesAsync();
                    }
                };
                await ShowErrorWithConnectAction(
                    "Not Connected",
                    "Connect to your Xbox before refreshing packages.",
                    "Go to the sidebar and connect to your Xbox Developer Mode console.",
                    connectAndRetry);
            }
            else if (ShowErrorAction is not null)
            {
                await ShowErrorAction(
                    "Not Connected",
                    "Connect to your Xbox before refreshing packages.",
                    "Go to the sidebar and connect to your Xbox Developer Mode console.");
            }
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        Logger.Info("Refreshing installed packages...");

        try
        {
            var packages = await _packageService.GetInstalledPackagesAsync();

            _allPackages.Clear();
            _allPackages.AddRange(packages
                .Where(p => p.Publisher is null ||
                            !p.Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name));

            Logger.Info($"Total packages from Xbox: {packages.Count}, after system filter: {_allPackages.Count}");

            SyncAutostartFlags();
            SyncIgnoreUpdateFlags();

            foreach (var pkg in _allPackages)
                Logger.Info($"  {pkg.Name,-30} v{pkg.Version,-14}  {pkg.DisplayPublisher ?? "-",-20}  {pkg.PackageFamilyName ?? ""}");

            SelectedPackage = null;
            ApplyFilter();

            if (_genericBanner is null)
            {
                try
                {
                    var uri = new Uri("avares://XBVault/Assets/Views/InstalledView/installed-banner-generic.jpg");
                    _genericBanner = new Bitmap(AssetLoader.Open(uri));
                }
                catch (Exception ex)
                {
                    // Banner asset missing/broken — leave null, views fall back to placeholder styling
                    Logger.Trace($"InstalledViewModel: failed to load generic banner — {ex.Message}");
                }
            }

            var bannerOpts = new ParallelOptions { MaxDegreeOfParallelism = 4 };
            await Parallel.ForEachAsync(_allPackages, bannerOpts, async (pkg, ct) =>
            {
                pkg.BannerImage = ResolveBannerAsync is not null
                    ? await ResolveBannerAsync(pkg) ?? _genericBanner
                    : _genericBanner;
            });

            await RefreshRunningStateAsync();

            if (CheckOutdatedAsync is not null)
            {
                foreach (var pkg in _allPackages)
                {
                    try
                    {
                        var (_, outdated) = await CheckOutdatedAsync(pkg);
                        pkg.IsOutdated = outdated;
                    }
                    catch (Exception ex)
                    {
                        // Version-check failure (offline, console busy) — keep IsOutdated=false rather than failing the whole refresh
                        Logger.Trace($"InstalledViewModel: outdated check failed for {pkg.Name} — {ex.Message}");
                    }
                }
            }

            LastUpdated = "Updated: " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            Logger.Info($"User-installed packages shown: {Packages.Count}, running: {_allPackages.Count(p => p.IsRunning)}");

            if (!HasPackages)
                StatusMessage = "No packages installed or Xbox not connected";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load packages";
            Logger.Error(ex, "Refresh packages failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Func<InstalledPackage, Task<bool>>? ConfirmUninstallAsync { get; set; }

    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        if (SelectedPackage is null) return;

        var pkg = SelectedPackage;
        if (ConfirmUninstallAsync is not null)
        {
            var ok = await ConfirmUninstallAsync(pkg);
            if (!ok) return;
        }

        IsUninstalling = true;
        pkg.IsUninstalling = true;
        Logger.Info($"Uninstalling: {pkg.Name}");

        try
        {
            var result = await _packageService.UninstallPackageAsync(pkg.FullName);
            Logger.Info(result ? $"Uninstall complete: {pkg.Name}" : $"Uninstall failed: {pkg.Name}");
            await RefreshPackagesAsync();
        }
        catch (Exception ex)
        {
            pkg.IsUninstalling = false;
            StatusMessage = "Uninstall failed";
            Logger.Error(ex, $"Uninstall error: {pkg.Name}");
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    [RelayCommand]
    private async Task InstallPackageAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        IsLoading = true;
        StatusMessage = $"Installing {Path.GetFileName(filePath)}...";
        Logger.Info($"Installing package: {filePath}");

        try
        {
            var result = await _packageService.InstallPackageAsync(filePath);
            StatusMessage = result ? "Install complete" : "Install failed";

            if (result)
                Logger.Info("Install via file complete");
            else
                Logger.Error("Install via file failed");

            await RefreshPackagesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Install failed";
            Logger.Error(ex, "Install via file error");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
