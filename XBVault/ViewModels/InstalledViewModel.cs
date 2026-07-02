using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public partial class InstalledViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private readonly List<InstalledPackage> _allPackages = [];

    public Func<string, Task>? OpenCustomInstallWithFileAction { get; set; }

    public InstalledViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        _xboxService.ConnectionChanged += OnConnectionChanged;
        IsConnected = _xboxService.IsConnected;
        Logger.Debug("InstalledViewModel initialized");
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
        if (connected)
            StatusMessage = null;
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
            LastUpdated = "Updated: " + DateTime.Now.ToString("HH:mm:ss");
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
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
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
        foreach (var pkg in filtered)
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
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisconnected));
        OnPropertyChanged(nameof(ShowRefreshPrompt));
        OnPropertyChanged(nameof(ShowGrid));
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

    public bool IsPackageSelected => SelectedPackage is not null && !IsUninstalling;
    public bool IsPackageRunning => !IsLoading && !IsUninstalling && (SelectedPackage?.IsRunning ?? false);
    public bool IsPackageNotRunning => !IsLoading && !IsUninstalling && (SelectedPackage is null || !SelectedPackage.IsRunning);
    public bool IsPackageSelectedNotRunning => !IsLoading && !IsUninstalling && SelectedPackage is not null && !SelectedPackage.IsRunning;
    public bool CanRefresh => !IsUninstalling && IsConnected;

    partial void OnSelectedPackageChanged(InstalledPackage? value)
    {
        OnPropertyChanged(nameof(IsPackageSelected));
        OnPropertyChanged(nameof(IsPackageRunning));
        OnPropertyChanged(nameof(IsPackageNotRunning));
        OnPropertyChanged(nameof(IsPackageSelectedNotRunning));
        if (value is not null)
        {
            Logger.Info($"Selected package raw:\n{value.RawJson}");
        }
    }

    private void UpdateRunningState()
    {
        OnPropertyChanged(nameof(IsPackageRunning));
        OnPropertyChanged(nameof(IsPackageNotRunning));
        OnPropertyChanged(nameof(IsPackageSelectedNotRunning));
    }

    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Func<string, string, string, Task>? ShowErrorAction { get; set; }
    public Func<InstalledPackage, Bitmap?>? ResolveBanner { get; set; }
    public Action? OnCatalogReady { get; set; }

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
                _xboxService.MarkConnected();
        }
    }

    private async Task<bool> SuspendAnyRunningAsync(InstalledPackage? excludePkg = null)
    {
        var running = _allPackages.FirstOrDefault(p => p.IsRunning && p != excludePkg);
        if (running is null) return true;

        var ok = await _xboxService.SuspendPackageAsync(running.FullName);
        if (ok)
        {
            running.IsRunning = false;
            UpdateRunningState();
            Logger.Info($"Suspended {running.Name} before launch");
            return true;
        }

        Logger.Warn($"Failed to suspend {running.Name}, will refresh");
        return false;
    }

    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        if (SelectedPackage is null || SelectedPackage.IsRunning) return;

        var rid = SelectedPackage.PackageRelativeId;
        if (string.IsNullOrEmpty(rid))
        {
            ToolbarStatus = "Cannot launch: no package relative id";
            return;
        }

        if (!await SuspendAnyRunningAsync(SelectedPackage))
        {
            await RefreshPackagesAsync();
            return;
        }

        try
        {
            var (ok, err) = await _xboxService.LaunchPackageAsync(SelectedPackage.FullName, rid);
            if (ok)
            {
                SelectedPackage.IsRunning = true;
                ToolbarStatus = $"Launched: {SelectedPackage.Name}";
                UpdateRunningState();
                _ = RefreshRunningStateAsync();
            }
            else
            {
                ToolbarStatus = $"Failed: {err ?? "unknown error"}";
            }
        }
        catch (Exception ex)
        {
            ToolbarStatus = "Launch failed";
            Logger.Error(ex, $"Launch failed for {SelectedPackage.Name}");
        }
    }

    [RelayCommand]
    private async Task SuspendSelectedAsync()
    {
        if (SelectedPackage is null || !SelectedPackage.IsRunning) return;
        var ok = await _xboxService.SuspendPackageAsync(SelectedPackage.FullName);
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
        var ok = await _xboxService.TerminatePackageAsync(SelectedPackage.FullName);
        if (ok)
        {
            SelectedPackage.IsRunning = false;
            ToolbarStatus = $"Terminated: {SelectedPackage.Name}";
            UpdateRunningState();
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

        var rid = pkg.PackageRelativeId;
        if (string.IsNullOrEmpty(rid))
        {
            ToolbarStatus = "Cannot launch: no package relative id";
            return;
        }

        if (!await SuspendAnyRunningAsync(pkg))
        {
            await RefreshPackagesAsync();
            return;
        }

        try
        {
            var (ok, err) = await _xboxService.LaunchPackageAsync(pkg.FullName, rid);
            if (ok)
            {
                pkg.IsRunning = true;
                ToolbarStatus = $"Launched: {pkg.Name}";
                _ = RefreshRunningStateAsync();
            }
            else
            {
                ToolbarStatus = $"Failed: {err ?? "unknown error"}";
            }
        }
        catch (Exception ex)
        {
            ToolbarStatus = "Launch failed";
            Logger.Error(ex, $"Launch failed for {pkg.Name}");
        }
    }

    [RelayCommand]
    private async Task SuspendPackageAsync(InstalledPackage pkg)
    {
        if (pkg is null || !pkg.IsRunning) return;

        var ok = await _xboxService.SuspendPackageAsync(pkg.FullName);
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

        var ok = await _xboxService.TerminatePackageAsync(pkg.FullName);
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
        var running = await _xboxService.GetRunningPackageNamesAsync();

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
        if (!_xboxService.IsConnected)
        {
            Logger.Info("Xbox not connected — showing error dialog");
            if (ShowErrorAction is not null)
                await ShowErrorAction(
                    "Not Connected",
                    "Connect to your Xbox before refreshing packages.",
                    "Go to the sidebar and connect to your Xbox Developer Mode console.");
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        Logger.Info("Refreshing installed packages...");

        try
        {
            var packages = await _xboxService.GetInstalledPackagesAsync();

            _allPackages.Clear();
            _allPackages.AddRange(packages
                .Where(p => p.Publisher is null ||
                            !p.Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name));

            Logger.Info($"Total packages from Xbox: {packages.Count}, after system filter: {_allPackages.Count}");

            foreach (var pkg in _allPackages)
                Logger.Info($"  {pkg.Name,-30} v{pkg.Version,-14}  {pkg.DisplayPublisher ?? "-",-20}  {pkg.PackageFamilyName ?? ""}");

            ApplyFilter();

            if (_genericBanner is null)
            {
                try
                {
                    var uri = new Uri("avares://XBVault/Assets/Views/InstalledView/installed-banner-generic.jpg");
                    _genericBanner = new Bitmap(AssetLoader.Open(uri));
                }
                catch { }
            }

            foreach (var pkg in _allPackages)
            {
                pkg.BannerImage = ResolveBanner?.Invoke(pkg) ?? _genericBanner;
            }

            await RefreshRunningStateAsync();
            LastUpdated = "Updated: " + DateTime.Now.ToString("HH:mm:ss");
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
            var result = await _xboxService.UninstallPackageAsync(pkg.FullName);
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
            var result = await _xboxService.InstallPackageAsync(filePath);
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
