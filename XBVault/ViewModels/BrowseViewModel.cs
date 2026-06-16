using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    private readonly EmulationRevivalService _erService;
    private readonly PackageInstallService _installService;
    private readonly XboxDeviceService _xboxService;
    private List<CatalogItem> _allItems = [];

    public Action<CatalogItem>? ShowDetailAction;
    public Func<Task>? ShowRefreshDialogAsync;

    private static readonly HttpClient ImageHttp = new();

    public BrowseViewModel(EmulationRevivalService erService, PackageInstallService installService, XboxDeviceService xboxService)
    {
        _erService = erService;
        _installService = installService;
        _xboxService = xboxService;
        Categories =
        [
            "All",
            "Emulator",
            "Frontend",
            "GamePort",
            "App",
            "Experimental",
            "Media",
            "Utility"
        ];
        Logger.Debug($"BrowseViewModel created, {Categories.Count} categories");
    }

    public ObservableCollection<CatalogItem> Items { get; } = [];
    public List<string> Categories { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string? _installStatus;

    [ObservableProperty]
    private bool _showExperimental = true;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private CatalogItem? _selectedItem;

    [ObservableProperty]
    private string? _installedVersion;

    [ObservableProperty]
    private bool _isCheckingInstalled;

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
        InstalledVersion = null;
        Logger.Info($"Checking install status for [{item.Category}] {item.Name}");

        Logger.Debug($"XboxDeviceService.IsConfigured={_xboxService.IsConfigured}");
        if (!_xboxService.IsConfigured)
        {
            InstalledVersion = "Not connected";
            Logger.Info("Xbox not configured — skipping installed check");
            IsCheckingInstalled = false;
            return;
        }

        try
        {
            Logger.Debug("Fetching installed packages from Xbox...");
            var packages = await _xboxService.GetInstalledPackagesAsync();
            Logger.Debug($"Got {packages.Count} installed packages from Xbox");

            var match = packages.FirstOrDefault(p =>
                p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            InstalledVersion = match?.Version ?? "Not installed";

            if (match is not null)
                Logger.Info($"Found installed: {item.Name} v{match.Version} ({match.InstalledSizeBytes} bytes)");
            else
                Logger.Info($"Not installed: {item.Name}");
        }
        catch (Exception ex)
        {
            InstalledVersion = "Check failed";
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
            InstallProgress = 0;
            InstallStatus = null;
            Logger.Debug($"Item selected: [{value.Category}] {value.Name} v{value.Version}");
            ShowDetailAction?.Invoke(value);
        }
    }

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        IsLoading = true;
        Logger.Info("Loading catalog from Emulation Revival...");

        try
        {
            Logger.Debug("FetchCatalogAsync start");
            _allItems = await _erService.FetchCatalogAsync(forceRefresh: false);
            Logger.Info($"Catalog loaded: {_allItems.Count} items total");

            var byCategory = _allItems.GroupBy(i => i.Category)
                .Select(g => $"{g.Key}={g.Count()}");
            Logger.Debug($"Per category: {string.Join(", ", byCategory)}");

            ApplyFilters();
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
        Logger.Info("RefreshCatalog command triggered — opening refresh dialog");

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
                _allItems = await _erService.FetchCatalogAsync(forceRefresh: true);
                Logger.Info($"Catalog refreshed: {_allItems.Count} items total");
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
    private async Task InstallSelectedAsync()
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

        IsInstalling = true;
        InstallProgress = 0;
        InstallStatus = "Downloading...";
        Logger.Info($"Install starting: {SelectedItem.Name} from {SelectedItem.DownloadUrl}");

        var progress = new Progress<double>(p =>
        {
            InstallProgress = p;
            if (p < 0.5)
                InstallStatus = "Downloading...";
            else if (p < 0.6)
                InstallStatus = "Analyzing package...";
            else if (p < 1.0)
                InstallStatus = "Installing on Xbox...";
            else
                InstallStatus = "Complete!";
        });

        Logger.Debug("Calling DownloadAndInstallAsync");
        var result = await _installService.DownloadAndInstallAsync(SelectedItem, progress);

        if (result)
        {
            InstallStatus = "Complete!";
            Logger.Info($"Install complete: {SelectedItem.Name}");
        }
        else
        {
            InstallStatus = "Install failed";
            Logger.Error($"Install failed: {SelectedItem.Name}");
        }

        InstallProgress = result ? 1.0 : 0;
        IsInstalling = false;
        Logger.Debug("Install flow finished");
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
                Logger.Trace($"Fetching thumbnail: {item.ImageUrl}");
                var bytes = await ImageHttp.GetByteArrayAsync(item.ImageUrl);
                using var ms = new MemoryStream(bytes);
                item.Thumbnail = new Bitmap(ms);
                loaded++;

                var idx = Items.IndexOf(item);
                if (idx >= 0)
                {
                    Items.RemoveAt(idx);
                    Items.Insert(idx, item);
                }
            }
            catch (Exception ex)
            {
                Logger.Trace($"Thumbnail failed for {item.Name}: {ex.Message}");
            }
        }

        Logger.Debug($"Thumbnails loaded: {loaded}/{total}");
    }
}
