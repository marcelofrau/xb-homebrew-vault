using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    private const int SlowThumbnailDelayMs = 3000;

    private readonly CatalogApiService _catalogService;
    private readonly PackageInstallService _installService;
    private readonly XboxDeviceService _xboxService;
    private readonly PackageOverrideService _overrideService;
    private List<CatalogItem> _allItems = [];

    public Action<CatalogItem>? ShowDetailAction;
    public Action? CloseDetailAction;
    public Action? ShowCustomInstallAction;
    public Func<string, Task>? OpenCustomInstallWithFileAction;
    public Action? OnCatalogLoaded;

    [RelayCommand]
    private void CloseDetail() => CloseDetailAction?.Invoke();

    [RelayCommand]
    private void OpenCustomInstall() => ShowCustomInstallAction?.Invoke();

    public Func<Task>? ShowRefreshDialogAsync;

    private static readonly HttpClient ImageHttp = new();
    private readonly ConcurrentDictionary<string, Bitmap?> _overrideImageCache = new();

    public BrowseViewModel(PackageInstallService installService, XboxDeviceService xboxService, CatalogApiService catalogService, PackageOverrideService overrideService)
    {
        _catalogService = catalogService;
        _installService = installService;
        _xboxService = xboxService;
        _overrideService = overrideService;
        Logger.Debug("BrowseViewModel created");
    }

    public ObservableCollection<CatalogItem> Items { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All"];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
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
    private bool _showExperimental = true;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private CatalogItem? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isCheckingInstalled;

    public Cursor? Cursor => (IsLoading || IsInstalling || IsCheckingInstalled) ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    [ObservableProperty]
    private bool _checkComplete;

    [ObservableProperty]
    private bool _checkInstalled;

    [ObservableProperty]
    private bool _checkError;

    [ObservableProperty]
    private string? _checkResultMessage;

    [ObservableProperty]
    private string? _installResultMessage;

    public bool IsNotInstalling => !IsInstalling;
    public bool CanCheckInstalled => !IsInstalling && !IsCheckingInstalled;
    public bool ShowWindowsToolBanner => SelectedItem?.IsWindowsTool == true;
    public bool CanInstallXboxItem => IsNotInstalling && !ShowWindowsToolBanner;
    public bool CanCheckXboxItem => CanCheckInstalled && !ShowWindowsToolBanner;
    public bool CanRecheckXboxItem => CanRecheck && !ShowWindowsToolBanner;
    public bool ShowDescriptionPanel => !IsInstalling && !InstallComplete && !IsCheckingInstalled && !CheckComplete;
    public bool ShowInstallOverlay => IsInstalling || InstallComplete;
    public bool ShowCheckOverlay => IsCheckingInstalled || CheckComplete;
    public bool CanRecheck => CheckComplete && !IsCheckingInstalled;
    public bool ShowCheckNotInstalled => CheckComplete && !CheckInstalled && !CheckError;
    public bool ShowCheckNotDetectedHint => ShowCheckNotInstalled;
    public bool ShowCheckNotConnectedHint => CheckComplete && CheckError && CheckResultMessage == "Not connected";
    public string? CheckVersionHint => CheckInstalled ? $"Available: {SelectedItem?.Version}" : null;

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotInstalling));
        OnPropertyChanged(nameof(CanCheckInstalled));
        OnPropertyChanged(nameof(CanInstallXboxItem));
        OnPropertyChanged(nameof(CanCheckXboxItem));
        OnPropertyChanged(nameof(CanRecheckXboxItem));
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(ShowInstallOverlay));
    }

    partial void OnInstallCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(ShowInstallOverlay));
    }

    partial void OnCheckCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(ShowCheckOverlay));
        OnPropertyChanged(nameof(CanRecheck));
        OnPropertyChanged(nameof(CanRecheckXboxItem));
        OnPropertyChanged(nameof(ShowCheckNotInstalled));
        OnPropertyChanged(nameof(ShowCheckNotDetectedHint));
        OnPropertyChanged(nameof(ShowCheckNotConnectedHint));
        OnPropertyChanged(nameof(CheckVersionHint));
    }

    partial void OnCheckInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(CheckVersionHint));
    }

    partial void OnIsCheckingInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckInstalled));
        OnPropertyChanged(nameof(CanCheckXboxItem));
        OnPropertyChanged(nameof(CanRecheckXboxItem));
        OnPropertyChanged(nameof(ShowCheckOverlay));
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(CanRecheck));
        OnPropertyChanged(nameof(ShowCheckNotInstalled));
        OnPropertyChanged(nameof(ShowCheckNotDetectedHint));
        OnPropertyChanged(nameof(ShowCheckNotConnectedHint));
    }

    [RelayCommand]
    private async Task CheckInstalledAsync()
    {
        var item = SelectedItem;
        if (item is null)
        {
            Logger.Warn("CheckInstalled called with no selected item");
            return;
        }

        IsCheckingInstalled = true;
        CheckComplete = false;
        CheckInstalled = false;
        CheckError = false;
        CheckResultMessage = null;
        InstallComplete = false;
        Logger.Info($"Checking install status for [{item.Category}] {item.Name}");

        if (item.IsWindowsTool)
        {
            CheckComplete = true;
            CheckResultMessage = "Windows tool — not an Xbox package";
            Logger.Info("Skipping check for Windows tool");
            IsCheckingInstalled = false;
            return;
        }

        if (!_xboxService.IsConfigured)
        {
            CheckComplete = true;
            CheckError = true;
            CheckResultMessage = "Not configured";
            Logger.Info("Xbox not configured — skipping installed check");
            IsCheckingInstalled = false;
            return;
        }
        if (!_xboxService.IsConnected)
        {
            CheckComplete = true;
            CheckError = true;
            CheckResultMessage = "Not connected";
            Logger.Info("Xbox not connected — skipping installed check");
            IsCheckingInstalled = false;
            return;
        }

        try
        {
            Logger.Debug("Fetching installed packages from Xbox...");
            var packages = await _xboxService.GetInstalledPackagesAsync();
            Logger.Debug($"Got {packages.Count} installed packages from Xbox");

            var match = packages.FirstOrDefault(p => IsPackageMatch(item, p));
            CheckComplete = true;

            if (match is not null)
            {
                CheckInstalled = true;
                CheckResultMessage = match.Version;
                Logger.Info($"Found installed: {item.Name} v{match.Version}");
            }
            else
            {
                CheckResultMessage = "Not installed";
                Logger.Info($"Not installed: {item.Name}");
            }
        }
        catch (Exception ex)
        {
            CheckComplete = true;
            CheckError = true;
            CheckResultMessage = "Check failed";
            Logger.Error(ex, $"Check installed failed for {item.Name}");
        }
        finally
        {
            IsCheckingInstalled = false;
            Logger.Debug("CheckInstalled completed");
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnShowExperimentalChanged(bool value) => ApplyFilters();

    partial void OnSelectedItemChanged(CatalogItem? value)
    {
        if (value is not null)
        {
            IsInstalling = false;
            IsCheckingInstalled = false;
            InstallComplete = false;
            InstallSuccess = false;
            InstallProgress = 0;
            InstallStatus = null;
            InstallResultMessage = null;
            CheckComplete = false;
            CheckInstalled = false;
            CheckError = false;
            CheckResultMessage = null;
            OnPropertyChanged(nameof(ShowWindowsToolBanner));
            OnPropertyChanged(nameof(CanInstallXboxItem));
            OnPropertyChanged(nameof(CanCheckXboxItem));
            OnPropertyChanged(nameof(CanRecheckXboxItem));
            Logger.Info($"Item selected: [{value.Category}] {value.Name} v{value.Version}");
            if (ShowDetailAction is null)
                Logger.Info("ShowDetailAction is NULL — detail window will not open");
            ShowDetailAction?.Invoke(value);
        }
    }

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
            OnCatalogLoaded?.Invoke();

            foreach (var item in _allItems)
                Logger.Info($"  {item.Name}");

            var byCategory = _allItems.GroupBy(i => i.Category)
                .Select(g => $"{g.Key}={g.Count()}");
            Logger.Debug($"Per category: {string.Join(", ", byCategory)}");

            RebuildCategories();
            ApplyFilters();
            IsLoading = false;
            _ = LoadThumbnailsAsync();
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

                foreach (var item in _allItems)
                    Logger.Info($"  {item.Name}");

                ApplyFilters();
                _ = LoadThumbnailsAsync();
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
        OpenUrl(url);
    }

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.Warn("OpenLink called with empty URL");
            return;
        }
        OpenUrl(url);
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
        if (!_xboxService.IsConnected)
        {
            Logger.Info("Xbox not connected — cannot install");
            CheckComplete = false;
            CheckInstalled = false;
            CheckError = false;
            CheckResultMessage = null;
            InstallComplete = true;
            InstallSuccess = false;
            InstallResultMessage = "Not connected. Connect via sidebar first.";
            return;
        }

        var itemName = SelectedItem.Name;
        var itemUrl = downloadUrl ?? "?";

        CheckComplete = false;
        CheckInstalled = false;
        CheckError = false;
        CheckResultMessage = null;
        IsInstalling = true;
        InstallComplete = false;
        InstallResultMessage = null;
        InstallProgress = 0;
        PackageProgress = 0;
        InstallStatus = "";
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
            var result = await _installService.DownloadAndInstallAsync(SelectedItem!, downloadUrl, progress);

            if (result)
            {
                InstallStatus = "✓ Complete!";
                InstallComplete = true;
                InstallSuccess = true;
                InstallResultMessage = null;
                Logger.Info($"Install complete: {itemName}");
            }
            else
            {
                InstallStatus = "✗ Install failed";
                InstallComplete = true;
                InstallSuccess = false;
                InstallResultMessage = "Install failed";
                Logger.Error($"Install failed: {itemName}");
            }

            InstallProgress = result ? 1.0 : 0;
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
            var q = SearchText.ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Name.ToLowerInvariant().Contains(q) ||
                i.Description.ToLowerInvariant().Contains(q) ||
                (i.Developer?.ToLowerInvariant().Contains(q) ?? false));
        }

        if (SelectedCategory != "All")
            filtered = filtered.Where(i => i.Category == SelectedCategory);

        if (!ShowExperimental)
            filtered = filtered.Where(i => !i.IsExperimental);

        foreach (var item in filtered)
            Items.Add(item);

        HasItems = Items.Count > 0;
        Logger.Debug($"Filters applied: cat={SelectedCategory} search='{SearchText}' → {Items.Count} items");
    }

#if DEBUG
    public static bool SlowThumbnails { get; set; }
#endif

    private async Task LoadThumbnailsAsync()
    {
        var total = _allItems.Count(i => !string.IsNullOrEmpty(i.ImageUrl) && i.Thumbnail is null);
        Logger.Debug($"Loading {total} thumbnails");

        int loaded = 0;
        foreach (var item in _allItems)
        {
            if (string.IsNullOrEmpty(item.ImageUrl) || item.Thumbnail is not null)
                continue;

            try
            {
#if DEBUG
                if (SlowThumbnails)
                    await Task.Delay(SlowThumbnailDelayMs);
#endif
                Logger.Trace($"Fetching thumbnail: {item.ImageUrl}");
                var bytes = await ImageHttp.GetByteArrayAsync(item.ImageUrl);
                using var ms = new MemoryStream(bytes);
                item.Thumbnail = new Bitmap(ms);
                loaded++;
            }
            catch (Exception ex)
            {
                Logger.Trace($"Thumbnail failed for {item.Name}: {ex.Message}");
            }
        }

        Logger.Debug($"Thumbnails loaded: {loaded}/{total}");
    }

    public Bitmap? FindThumbnailByPackage(InstalledPackage pkg)
    {
        var match = _allItems.FirstOrDefault(i => IsPackageMatch(i, pkg));
        if (match?.Thumbnail is not null)
            return match.Thumbnail;

        var url = GetOverrideImageUrl(pkg);
        if (url is null) return null;

        return _overrideImageCache.GetOrAdd(url, u =>
        {
            try
            {
                var bytes = Task.Run(() => ImageHttp.GetByteArrayAsync(u)).GetAwaiter().GetResult();
                using var ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        });
    }

    private string? GetOverrideImageUrl(InstalledPackage pkg)
    {
        var pfn = !string.IsNullOrEmpty(pkg.PackageFamilyName) ? StripPackageFamilyName(pkg.PackageFamilyName) : null;
        if (pfn is not null && _overrideService.TryGetImageUrl(pfn, out var url))
            return url;
        if (_overrideService.TryGetImageUrlByName(pkg.Name, out var url2))
            return url2;
        if (!string.IsNullOrEmpty(pkg.DisplayName) && _overrideService.TryGetImageUrlByName(pkg.DisplayName, out var url3))
            return url3;
        return null;
    }

    private bool IsPackageMatch(CatalogItem catalog, InstalledPackage pkg)
    {
        // E0: Exact matches (highest confidence)
        if (catalog.Name.Equals(pkg.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(pkg.DisplayName) && catalog.Name.Equals(pkg.DisplayName, StringComparison.OrdinalIgnoreCase))
            return true;

        var pfn = !string.IsNullOrEmpty(pkg.PackageFamilyName) ? StripPackageFamilyName(pkg.PackageFamilyName) : null;

        if (pfn is not null && catalog.Name.Equals(pfn, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(catalog.AppId) && pkg.Name.Contains(catalog.AppId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(catalog.Id) && pkg.Name.Contains(catalog.Id, StringComparison.OrdinalIgnoreCase))
            return true;

        // E1: Alphanumeric normalization — strip non-alnum, lowercase
        // Handles "Super Mario Bros Remastered" vs "SuperMarioBrosRemastered" (spaces)
        var catNorm = NormalizeAlnum(catalog.Name);
        if (pfn is not null && catNorm.Equals(NormalizeAlnum(pfn), StringComparison.OrdinalIgnoreCase))
            return true;

        // E2: Download URL filename contains package Name or DisplayName
        // Handles SMBR: downloadUrl "SMBR_1.2.zip" contains Name "SMBR"
        if (DownloadUrlContains(pkg.Name, catalog) ||
            (!string.IsNullOrEmpty(pkg.DisplayName) && DownloadUrlContains(pkg.DisplayName, catalog)))
            return true;

        // E3: Reverse — catalog.AppId contains normalized PFN
        // Handles cases where PFN is abbreviation embedded in appId
        if (!string.IsNullOrEmpty(catalog.AppId) && pfn is not null &&
            catalog.AppId.Contains(pfn, StringComparison.OrdinalIgnoreCase))
            return true;

        // E4: Download URL filename first-token prefix of PFN (or vice versa), minimum 4 chars
        // Handles SMWRP: url firstToken "SMWR" is prefix of PFN "SMWRP"
        if (pfn is not null && DownloadTokenPrefixMatch(pfn, catalog))
            return true;

        // E5: Manual override table (final fallback — zero false positives)
        if (!string.IsNullOrEmpty(pfn) && _overrideService.TryGetCatalogId(pfn, out var overrideId))
        {
            if (catalog.Id.Equals(overrideId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (_overrideService.TryGetCatalogIdByName(pkg.Name, out var overrideIdByName))
        {
            if (catalog.Id.Equals(overrideIdByName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string StripPackageFamilyName(string familyName)
    {
        var idx = familyName.LastIndexOf('_');
        return idx > 0 ? familyName[..idx] : familyName;
    }

    private static string NormalizeAlnum(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(value, "[^a-zA-Z0-9]", "");
    }

    private static bool DownloadUrlContains(string value, CatalogItem catalog)
    {
        if (string.IsNullOrEmpty(value)) return false;

        var urls = new[] { catalog.DownloadUrl }
            .Concat(catalog.Downloads.Select(d => d.Url))
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct();

        foreach (var url in urls)
        {
            var filename = Path.GetFileNameWithoutExtension(url);
            if (filename is not null && filename.Contains(value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool DownloadTokenPrefixMatch(string pfn, CatalogItem catalog)
    {
        var urls = new[] { catalog.DownloadUrl }
            .Concat(catalog.Downloads.Select(d => d.Url))
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct();

        foreach (var url in urls)
        {
            var filename = Path.GetFileNameWithoutExtension(url);
            if (string.IsNullOrEmpty(filename)) continue;

            var token = filename.Split('_')[0];
            if (token.Length < 4 && pfn.Length < 4) continue;

            if (pfn.StartsWith(token, StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith(pfn, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
