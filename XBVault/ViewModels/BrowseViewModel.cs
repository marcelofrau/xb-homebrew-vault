#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

/// <summary>
/// Coordinates catalog browsing, filtering, thumbnail loading, install checks, and package installation.
/// </summary>
/// <remarks>
/// The ViewModel is frontend-neutral: desktop and Android shells should provide dialogs, file pickers,
/// and navigation through delegate properties instead of moving catalog or install logic into Views.
/// </remarks>
public partial class BrowseViewModel : ObservableObject, IDisposable
{
    private const int SlowThumbnailDelayMs = 3000;
    private const int ThumbnailDecodeWidth = 520;

    private readonly CatalogApiService _catalogService;
    private readonly PackageInstallService _installService;
    private readonly IXboxAuthService _authService;
    private readonly IXboxPackageService _packageService;
    private readonly PackageOverrideService _overrideService;
    private readonly VersionCheckerService _versionChecker;
    public Action<string> OpenUrlAction { get; set; } = OpenUrl;
    private List<CatalogItem> _allItems = [];

    public Action<CatalogItem>? ShowDetailAction { get; set; }
    public Action? CloseDetailAction { get; set; }
    public Action? ShowCustomInstallAction { get; set; }
    public Func<string, Task>? OpenCustomInstallWithFileAction { get; set; }
    public Action? OnCatalogLoaded { get; set; }
    public Func<InstalledPackage, Task<bool>>? ConfirmUninstallAsync { get; set; }
    public Func<InstalledPackage, Task>? ReinstallFromDetailAction { get; set; }
    public Func<InstalledPackage, Task<(CatalogItem? match, bool isOutdated)>>? ResolveInstalledCatalogItem { get; set; }
    public Func<Task<bool>>? ShowConnectAction { get; set; }

    [RelayCommand]
    private void CloseDetail() => CloseDetailAction?.Invoke();

    [RelayCommand]
    private async Task UninstallFromDetail()
    {
        var pkg = SelectedInstalledPackage ?? SelectedItem?.MatchedInstalledPackage;
        if (pkg is null)
            return;

        if (ConfirmUninstallAsync is not null && !await ConfirmUninstallAsync(pkg))
            return;

        IsUninstallingDetail = true;
        UninstallDetailFailed = false;
        UninstallResultMessage = null;

        bool success = false;
        try
        {
            Logger.Info($"Uninstall from detail: {pkg.Name}");
            success = await _packageService.UninstallPackageAsync(pkg.FullName);
            if (success)
                await UpdateInstalledBadgesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Uninstall from detail failed: {pkg.Name}");
            success = false;
        }
        finally
        {
            IsUninstallingDetail = false;
        }

        if (success)
        {
            CloseDetailAction?.Invoke();
        }
        else
        {
            UninstallDetailFailed = true;
            UninstallResultMessage = $"Failed to uninstall \"{pkg.Name}\". Check the connection and try again.";
        }
    }

    [RelayCommand]
    private Task ReinstallFromDetail()
    {
        var pkg = SelectedInstalledPackage ?? SelectedItem?.MatchedInstalledPackage;
        return pkg is not null && ReinstallFromDetailAction is not null
            ? ReinstallFromDetailAction(pkg)
            : Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenCustomInstall() => ShowCustomInstallAction?.Invoke();

    [RelayCommand]
    private void AbortInstall()
    {
        if (_installCts is { IsCancellationRequested: false })
        {
            Logger.Info("User aborted install");
            _installCts.Cancel();
            InstallStatus = "Aborted by user";
            InstallComplete = true;
            InstallSuccess = false;
            InstallResultMessage = "Install aborted.";
        }
    }

    public Func<Task>? ShowRefreshDialogAsync { get; set; }

    private static readonly HttpClient ImageHttp = new();
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _overrideImageCache = new();
    private CancellationTokenSource? _thumbnailCts;
    private CancellationTokenSource? _installCts;

    public BrowseViewModel(PackageInstallService installService, IXboxAuthService authService, IXboxPackageService packageService, CatalogApiService catalogService, PackageOverrideService overrideService, VersionCheckerService versionChecker)
    {
        _catalogService = catalogService;
        _installService = installService;
        _authService = authService;
        _packageService = packageService;
        _overrideService = overrideService;
        _versionChecker = versionChecker;
        _authService.ConnectionChanged += OnConnectionChanged;
        IsConnected = _authService.IsConnected;
        Logger.Debug("BrowseViewModel created");
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
        async Task Work()
        {
            if (connected && _allItems.Count > 0)
            {
                Logger.Debug("Connection changed — refreshing installed badges");
                await UpdateInstalledBadgesAsync();
                if (SelectedItem?.MatchedInstalledPackage is not null && SelectedInstalledPackage is null)
                    SelectedInstalledPackage = SelectedItem.MatchedInstalledPackage;
            }
            else if (!connected)
            {
                foreach (var item in _allItems)
                {
                    item.IsInstalledOnXbox = false;
                    item.IsOutdatedOnXbox = false;
                }
                Logger.Debug("Disconnected — cleared installed badges");
            }
            RaiseDetailDerived();
        }

        Work().FireAndForget("BrowseViewModel.OnConnectionChanged");
    }

    public ObservableCollection<CatalogItem> Items { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All"];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    [NotifyPropertyChangedFor(nameof(ShowNoItems))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private double _packageProgress;

    [ObservableProperty]
    private string? _installStatus;

    [ObservableProperty]
    private string? _packageStatus;

    [ObservableProperty]
    private string? _currentFile;

    [ObservableProperty]
    private bool _installComplete;

    [ObservableProperty]
    private bool _installSuccess;

    [ObservableProperty]
    private string? _installResultMessage;

    [ObservableProperty]
    private bool _isUninstallingDetail;

    [ObservableProperty]
    private bool _uninstallDetailFailed;

    [ObservableProperty]
    private string? _uninstallResultMessage;


    [ObservableProperty]
    private bool _showExperimental = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoItems))]
    private bool _hasItems;

    public bool ShowNoItems => !IsLoading && !HasItems;

    [ObservableProperty]
    private CatalogItem? _selectedItem;

    [ObservableProperty]
    private InstalledPackage? _selectedInstalledPackage;

    [ObservableProperty]
    private bool _isConnected;

    // Derived detail state: the item-detail actions always follow the live
    // Xbox connection + installed state instead of explicit mode flags. The
    // installed-tab path provides the InstalledPackage directly; the browse
    // path resolves it through the badge refresh (MatchedInstalledPackage).
    public bool DetailNeedsConnect => SelectedItem is not null && !IsConnected && !IsInstalling && !InstallComplete && !IsUninstallingDetail;
    public bool ShowConnectButton => SelectedItem is not null && !IsConnected && !IsInstalling && !InstallComplete && !IsUninstallingDetail;
    public bool ShowDetailInstallButton => IsConnected && !IsInstalling && !InstallComplete && !IsUninstallingDetail && !ShowWindowsToolBanner && !DetailInstalled;
    public bool ShowDetailUpdateButton => IsConnected && !IsInstalling && !InstallComplete && !IsUninstallingDetail && !ShowWindowsToolBanner && DetailInstalled && DetailOutdated;
    public bool ShowDetailReinstallButton => IsConnected && !IsInstalling && !InstallComplete && !IsUninstallingDetail && !ShowWindowsToolBanner && DetailInstalled && !DetailOutdated;
    public bool ShowDetailUninstallButton => IsConnected && !IsInstalling && !InstallComplete && !IsUninstallingDetail && !ShowWindowsToolBanner && DetailInstalled;
    public bool ShowFinishButton => InstallComplete && InstallSuccess;

    private bool DetailInstalled => SelectedInstalledPackage is not null || SelectedItem?.IsInstalledOnXbox == true;

    private bool DetailOutdated
    {
        get
        {
            if (SelectedInstalledPackage is not null &&
                Version.TryParse(SelectedInstalledPackage.Version, out var installedV) &&
                SelectedItem is not null &&
                Version.TryParse(SelectedItem.Version, out var catalogV))
                return catalogV > installedV;
            return SelectedItem?.IsOutdatedOnXbox == true;
        }
    }

    public string? DetailStatusText
    {
        get
        {
            if (!IsConnected || SelectedItem is not { } item) return null;
            if (!DetailInstalled) return null;
            var installedVersion = SelectedInstalledPackage?.Version ?? item.MatchedInstalledPackage?.Version;
            installedVersion = string.IsNullOrWhiteSpace(installedVersion) ? null : installedVersion;
            if (DetailOutdated)
                return $"Update available — {installedVersion ?? "?"} → {FirstWord(item.Version)}";
            return installedVersion is null
                ? "Installed"
                : $"Installed — v{installedVersion}";
        }
    }

    private static string FirstWord(string? value)
        => string.IsNullOrWhiteSpace(value) ? "?" : value!.Split(' ', StringSplitOptions.TrimEntries)[0];

    public Cursor? Cursor => (IsLoading || IsInstalling) ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    public bool IsNotInstalling => !IsInstalling;
    public bool ShowWindowsToolBanner => SelectedItem?.IsWindowsTool == true;
    public bool CanInstallXboxItem => IsConnected && !IsInstalling && !InstallComplete && !ShowWindowsToolBanner;
    public bool ShowInstallSuccessResult => InstallComplete && InstallSuccess;
    public bool ShowInstallFailureResult => InstallComplete && !InstallSuccess;
    public bool IsBusy => IsInstalling;
    public bool ShowDescriptionPanel => !IsInstalling && !InstallComplete;
    public bool ShowInstallOverlay => IsInstalling || InstallComplete;

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotInstalling));
        OnPropertyChanged(nameof(CanInstallXboxItem));
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(ShowInstallOverlay));
        OnPropertyChanged(nameof(IsBusy));
        RaiseDetailDerived();
    }

    partial void OnInstallCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(ShowInstallOverlay));
        OnPropertyChanged(nameof(ShowInstallSuccessResult));
        OnPropertyChanged(nameof(ShowInstallFailureResult));
        RaiseDetailDerived();
    }

    partial void OnInstallSuccessChanged(bool value)
    {
        if (value && SelectedItem is not null)
            _versionChecker.MarkJustUpdated(SelectedItem.Name);
        OnPropertyChanged(nameof(ShowInstallSuccessResult));
        OnPropertyChanged(nameof(ShowInstallFailureResult));
        RaiseDetailDerived();

        // Refresh installed badges after install completes
        if (value)
            _ = UpdateInstalledBadgesAsync();
    }

    partial void OnIsConnectedChanged(bool value) => RaiseDetailDerived();

    partial void OnIsUninstallingDetailChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowUninstallOverlay));
        OnPropertyChanged(nameof(CanInstallXboxItem));
        RaiseDetailDerived();
    }

    partial void OnUninstallDetailFailedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowUninstallOverlay));
        RaiseDetailDerived();
    }

    partial void OnSelectedInstalledPackageChanged(InstalledPackage? value) => RaiseDetailDerived();

    partial void OnSelectedItemChanged(CatalogItem? value)
    {
        if (_prevSelectedItem is not null)
            _prevSelectedItem.IsSelected = false;
        _prevSelectedItem = value;
        if (value is null)
        {
            UnsubscribeSelectedItemBadges();
            return;
        }

        value.IsSelected = true;
        IsInstalling = false;
        InstallComplete = false;
        InstallSuccess = false;
        InstallProgress = 0;
        InstallStatus = null;
        InstallResultMessage = null;
        SelectedInstalledPackage = null;
        SubscribeSelectedItemBadges(value);
        OnPropertyChanged(nameof(ShowWindowsToolBanner));
        OnPropertyChanged(nameof(CanInstallXboxItem));
        RaiseDetailDerived();
        Logger.Info($"Item selected: [{value.Category}] {value.Name} v{value.Version}");
        if (ShowDetailAction is null)
            Logger.Info("ShowDetailAction is NULL — detail window will not open");
        ShowDetailAction?.Invoke(value);
    }

    private void SubscribeSelectedItemBadges(CatalogItem item)
    {
        UnsubscribeSelectedItemBadges();
        _badgeSubscribedItem = item;
        _selectedItemBadgesHandler ??= OnSelectedItemBadgeChanged;
        item.PropertyChanged += _selectedItemBadgesHandler;
    }

    private void UnsubscribeSelectedItemBadges()
    {
        if (_badgeSubscribedItem is not null && _selectedItemBadgesHandler is not null)
        {
            _badgeSubscribedItem.PropertyChanged -= _selectedItemBadgesHandler;
            _badgeSubscribedItem = null;
        }
    }

    private void OnSelectedItemBadgeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CatalogItem.IsInstalledOnXbox) or nameof(CatalogItem.IsOutdatedOnXbox) or nameof(CatalogItem.MatchedInstalledPackage))
            RaiseDetailDerived();
    }

    private void RaiseDetailDerived()
    {
        OnPropertyChanged(nameof(DetailNeedsConnect));
        OnPropertyChanged(nameof(ShowConnectButton));
        OnPropertyChanged(nameof(ShowDetailInstallButton));
        OnPropertyChanged(nameof(ShowDetailUpdateButton));
        OnPropertyChanged(nameof(ShowDetailReinstallButton));
        OnPropertyChanged(nameof(ShowDetailUninstallButton));
        OnPropertyChanged(nameof(ShowFinishButton));
        OnPropertyChanged(nameof(DetailStatusText));
        OnPropertyChanged(nameof(CanInstallXboxItem));
        OnPropertyChanged(nameof(ShowUninstallOverlay));
    }

    public bool ShowUninstallOverlay => IsUninstallingDetail || UninstallDetailFailed;

    [RelayCommand]
    private async Task ConnectFromDetailAsync()
    {
        if (ShowConnectAction is null)
        {
            Logger.Warn("ConnectFromDetail called but ShowConnectAction is null");
            return;
        }
        Logger.Info("ConnectFromDetail — opening connection");
        var ok = await ShowConnectAction();
        if (ok)
        {
            Logger.Info("ConnectFromDetail — connected, refreshing detail state");
            if (SelectedItem?.MatchedInstalledPackage is not null && SelectedInstalledPackage is null)
                SelectedInstalledPackage = SelectedItem.MatchedInstalledPackage;
        }
        RaiseDetailDerived();
    }

    /// <summary>
    /// Fetches installed packages from Xbox and cross-references all catalog items
    /// to set IsInstalledOnXbox/IsOutdatedOnXbox for badge display.
    /// </summary>
    private async Task UpdateInstalledBadgesAsync()
    {
        if (!_authService.IsConfigured || !_authService.IsConnected) return;

        try
        {
            Logger.Debug("Updating installed badges for catalog items...");
            var packages = await _packageService.GetInstalledPackagesAsync();
            var matched = 0;
            var outdated = 0;

            foreach (var item in _allItems)
            {
                var match = packages.FirstOrDefault(p => _versionChecker.IsPackageMatch(item, p));
                if (match is not null)
                {
                    item.IsInstalledOnXbox = true;
                    matched++;

                    var effectiveVer = item.Version ?? string.Empty;
                    if (Version.TryParse(match.Version, out var installedV) &&
                        Version.TryParse(effectiveVer, out var catalogV) &&
                        catalogV > installedV)
                    {
                        item.IsOutdatedOnXbox = true;
                        outdated++;
                    }
                    else
                    {
                        item.IsOutdatedOnXbox = false;
                    }
                    item.MatchedInstalledPackage = match;
                }
                else
                {
                    item.IsInstalledOnXbox = false;
                    item.IsOutdatedOnXbox = false;
                    item.MatchedInstalledPackage = null;
                }
            }

            Logger.Info($"Badge update: {matched} installed, {outdated} outdated of {_allItems.Count} catalog items");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to update installed badges: {ex.Message}");
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnShowExperimentalChanged(bool value) => ApplyFilters();

    private CatalogItem? _prevSelectedItem;
    private CatalogItem? _badgeSubscribedItem;
    private System.ComponentModel.PropertyChangedEventHandler? _selectedItemBadgesHandler;

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        IsLoading = true;
        Logger.Info("Loading catalog...");

        try
        {
            Logger.Debug("FetchCatalogAsync start (JSON API primary)");
            _allItems = await _catalogService.FetchCatalogAsync(forceRefresh: false);
            Logger.Info($"Catalog loaded: {_allItems.Count} items total");
            _versionChecker.SetCatalog(_allItems);
            OnCatalogLoaded?.Invoke();

            for (var i = 0; i < _allItems.Count; i++)
            {
                var item = _allItems[i];
                Logger.Trace($"Catalog item [{i + 1}/{_allItems.Count}]: [{item.Category}] {item.Name} v{item.Version}");
            }

            var byCategory = _allItems.GroupBy(i => i.Category)
                .Select(g => $"{g.Key}={g.Count()}");
            Logger.Debug($"Per category: {string.Join(", ", byCategory)}");

            RebuildCategories();
            ApplyFilters();
            IsLoading = false;
            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();
            _ = LoadThumbnailsAsync(_thumbnailCts.Token);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Catalog load failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        Logger.Info("RefreshCatalog command triggered — clearing cache and refreshing");

        // Clear cache to force fresh fetch
        CatalogApiService.ClearCache();

        _thumbnailCts?.Cancel();
        _thumbnailCts = new CancellationTokenSource();

        if (ShowRefreshDialogAsync is not null)
        {
            await ShowRefreshDialogAsync();
        }
        else
        {
            Logger.Warn("ShowRefreshDialogAsync not set — falling back to direct refresh");
            // Fallback: do inline if delegate not wired
            try
            {
                _allItems = await _catalogService.FetchCatalogAsync(forceRefresh: true);
                Logger.Info($"Catalog refreshed: {_allItems.Count} items total");
                _versionChecker.SetCatalog(_allItems);

                for (var i = 0; i < _allItems.Count; i++)
                {
                    var item = _allItems[i];
                    Logger.Trace($"Catalog refresh item [{i + 1}/{_allItems.Count}]: [{item.Category}] {item.Name} v{item.Version}");
                }

                ApplyFilters();
                _ = LoadThumbnailsAsync(_thumbnailCts.Token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Catalog refresh failed (fallback)");
            }
        }
    }

    [RelayCommand]
    private void SelectItem(CatalogItem? item)
    {
        SelectedItem = item;
    }

    [RelayCommand]
    private void VisitSite()
    {
        var url = SelectedItem?.DownloadUrl ?? SelectedItem?.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.Warn("VisitSite called but no URL");
            return;
        }
        OpenUrlAction(url);
    }

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.Warn("OpenLink called with empty URL");
            return;
        }
        OpenUrlAction(url);
    }

    private static void OpenUrl(string url)
    {
        Logger.Info($"Opening URL: {url}");
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            proc.Start();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to open URL: {url}");
        }
    }

    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        var asset = SelectedItem?.MainDownload ?? SelectedItem?.Downloads.FirstOrDefault();
        var url = asset?.Url ?? SelectedItem?.DownloadUrl;
        await InstallAsync(url);
    }

    public async Task InstallByAssetAsync(DownloadAsset? asset)
    {
        var url = asset?.Url ?? SelectedItem?.MainDownload?.Url ?? SelectedItem?.DownloadUrl;
        await InstallAsync(url);
    }

    private async Task InstallAsync(string? downloadUrl)
    {
        if (SelectedItem is null)
        {
            Logger.Warn("InstallSelected called with no item");
            return;
        }
        if (IsInstalling)
        {
            Logger.Warn($"Install already in progress for {SelectedItem.Name}");
            return;
        }
        if (SelectedItem.IsWindowsTool)
        {
            Logger.Warn($"Refusing install for Windows tool: {SelectedItem.Name}");
            InstallComplete = true;
            InstallSuccess = false;
            InstallResultMessage = "This is a Windows tool — not installable on Xbox.";
            return;
        }
        if (!await _authService.EnsureConnectedAsync())
        {
            Logger.Info("Xbox not connected — cannot install");
            InstallComplete = true;
            InstallSuccess = false;
            InstallResultMessage = "Not connected. Connect via sidebar first.";
            return;
        }

        var itemName = SelectedItem.Name;
        var itemUrl = downloadUrl ?? "?";

        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();

        IsInstalling = true;
        InstallComplete = false;
        InstallResultMessage = null;
        InstallProgress = 0;
        PackageProgress = 0;
        InstallStatus = "Preparing download...";
        PackageStatus = "";
        CurrentFile = "";
        Logger.Info($"Install starting: {itemName} from {itemUrl}");

        try
        {
            var progress = new Progress<InstallProgressInfo>(info =>
            {
                InstallProgress = info.Total;
                PackageProgress = info.File;
                PackageStatus = info.Status;
                CurrentFile = info.CurrentFile;

                InstallStatus = info.Status;
            });

            Logger.Debug("Calling DownloadAndInstallAsync");
            var result = await _installService.DownloadAndInstallAsync(SelectedItem!, downloadUrl, progress, _installCts?.Token ?? CancellationToken.None);

            if (_installCts is { IsCancellationRequested: true })
            {
                // User aborted — never overwrite the aborted UI state even when the
                // console-side install happened to complete before the cancel landed.
                Logger.Info($"Install abort confirmed after flow end: {itemName}");
                InstallProgress = 0;
            }
            else if (result.Success)
            {
                InstallStatus = "✓ Complete!";
                InstallComplete = true;
                InstallSuccess = true;
                InstallResultMessage = null;
                InstallProgress = 1.0;
                Logger.Info($"Install complete: {itemName}");
            }
            else
            {
                InstallStatus = "✗ Install failed";
                InstallComplete = true;
                InstallSuccess = false;
                InstallResultMessage = result.Message ?? "Install failed";
                InstallProgress = 0;
                Logger.Error($"Install failed: {itemName} (stage={result.Stage})");
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info($"Install cancelled: {itemName}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Install crashed for {itemName}");
            InstallComplete = true;
            InstallSuccess = false;
            InstallResultMessage = $"Unexpected error: {ex.Message}";
            InstallProgress = 0;
        }
        finally
        {
            IsInstalling = false;
            Logger.Debug("Install flow finished");
        }
    }

    private void RebuildCategories()
    {
        var cats = _allItems
            .Select(i => i.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        Categories.Clear();
        Categories.Add("All");
        foreach (var cat in cats)
            Categories.Add(cat);

        SelectedCategory = "All";
        Logger.Debug($"Categories rebuilt: {Categories.Count - 1} categories loaded");
    }

    private void ApplyFilters()
    {
        Items.Clear();

        var filtered = _allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(i =>
                i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (i.Developer?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (SelectedCategory != "All")
            filtered = filtered.Where(i => i.Category == SelectedCategory);

        if (!ShowExperimental)
            filtered = filtered.Where(i => !i.IsExperimental);

        filtered = filtered.OrderByDescending(i =>
        {
            if (string.IsNullOrWhiteSpace(i.ReleaseDate))
                return DateTime.MinValue;
            if (DateTime.TryParse(i.ReleaseDate, out var dt))
                return dt;
            return DateTime.MinValue;
        });

        var result = filtered.ToList();
        foreach (var item in result)
            Items.Add(item);

        HasItems = Items.Count > 0;
        Logger.Debug($"Filters applied: cat={SelectedCategory} search='{SearchText}' → {Items.Count} items");
    }

#if DEBUG
    public static bool SlowThumbnails { get; set; }
#endif

    private async Task LoadThumbnailsAsync(CancellationToken ct = default)
    {
        var pending = _allItems.Where(i => !string.IsNullOrEmpty(i.ImageUrl) && i.Thumbnail is null).ToList();
        var total = pending.Count;
        Logger.Debug($"Loading {total} thumbnails (immediate apply)");

        var loaded = 0;
        var cache = new CacheService();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(pending, options, async (item, token) =>
        {
            try
            {
#if DEBUG
                if (SlowThumbnails)
                    await Task.Delay(SlowThumbnailDelayMs, token);
#endif
                var url = item.ImageUrl!;

                var cached = await cache.TryLoadThumbnailDataAsync(url);
                Bitmap bitmap;
                if (cached is not null)
                {
                    using var ms = new MemoryStream(cached);
                    bitmap = Bitmap.DecodeToWidth(ms, ThumbnailDecodeWidth);
                }
                else
                {
                    Logger.Trace($"Fetching thumbnail: {url}");
                    var bytes = await ImageHttp.GetByteArrayAsync(url, token);
                    using var ms = new MemoryStream(bytes);
                    bitmap = Bitmap.DecodeToWidth(ms, ThumbnailDecodeWidth);
                    _ = cache.SaveThumbnailAsync(url, bytes);
                }

                // Apply immediately on UI thread so cards show images as they load
                var capturedItem = item;
                var capturedBitmap = bitmap;
                await XBVault.Helpers.UIHelpers.RunOnUIAsync(() =>
                {
                    capturedItem.Thumbnail = capturedBitmap;
                    return Task.CompletedTask;
                });

                Interlocked.Increment(ref loaded);
            }
            catch (OperationCanceledException)
            {
                Logger.Trace("Thumbnail loading cancelled");
            }
            catch (Exception ex)
            {
                Logger.Trace($"Thumbnail failed for {item.Name}: {ex.Message}");
            }
        });

        Logger.Debug($"Thumbnails loaded: {loaded}/{total}");
    }

    public async Task<Bitmap?> FindThumbnailByPackageAsync(InstalledPackage pkg)
    {
        var match = _versionChecker.FindCatalogMatch(pkg).match;
        if (match?.Thumbnail is not null)
            return match.Thumbnail;

        var url = GetOverrideImageUrl(pkg);
        if (url is null) return null;

        return await _overrideImageCache.GetOrAdd(url, FetchImageAsync);
    }

    public (CatalogItem? match, bool isOutdated) FindCatalogMatch(InstalledPackage pkg)
        => _versionChecker.FindCatalogMatch(pkg);

    private static async Task<Bitmap?> FetchImageAsync(string url)
    {
        try
        {
            var bytes = await ImageHttp.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(ms, ThumbnailDecodeWidth);
        }
        catch
        {
            return null;
        }
    }

    private string? GetOverrideImageUrl(InstalledPackage pkg)
    {
        var pfn = !string.IsNullOrEmpty(pkg.PackageFamilyName) ? VersionCheckerService.StripPackageFamilyName(pkg.PackageFamilyName) : null;
        if (pfn is not null && _overrideService.TryGetImageUrl(pfn, out var url))
            return url;
        if (_overrideService.TryGetImageUrlByName(pkg.Name, out var url2))
            return url2;
        if (!string.IsNullOrEmpty(pkg.DisplayName) && _overrideService.TryGetImageUrlByName(pkg.DisplayName, out var url3))
            return url3;
        return null;
    }

    public void Dispose()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
