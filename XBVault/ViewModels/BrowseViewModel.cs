using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    public Action? CloseDetailAction;
    public Action? ShowCustomInstallAction;

    [RelayCommand]
    private void CloseDetail() => CloseDetailAction?.Invoke();

    [RelayCommand]
    private void OpenCustomInstall() => ShowCustomInstallAction?.Invoke();

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
    private string? _installedVersion;

    [ObservableProperty]
    private bool _isCheckingInstalled;

    [ObservableProperty]
    private string? _installResultMessage;

    public bool IsNotInstalling => !IsInstalling;
    public bool CanCheckInstalled => !IsInstalling && !IsCheckingInstalled;
    public bool ShowDescriptionPanel => !IsInstalling && !InstallComplete;
    public bool ShowInstallOverlay => IsInstalling || InstallComplete;

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotInstalling));
        OnPropertyChanged(nameof(CanCheckInstalled));
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(ShowInstallOverlay));
    }

    partial void OnInstallCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDescriptionPanel));
        OnPropertyChanged(nameof(ShowInstallOverlay));
    }

    partial void OnIsCheckingInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckInstalled));
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
        InstalledVersion = null;
        Logger.Info($"Checking install status for [{item.Category}] {item.Name}");

        Logger.Debug($"XboxDeviceService.IsConfigured={_xboxService.IsConfigured}");
        if (!_xboxService.IsConfigured)
        {
            InstalledVersion = "Not configured";
            Logger.Info("Xbox not configured — skipping installed check");
            IsCheckingInstalled = false;
            return;
        }
        if (!_xboxService.IsConnected)
        {
            InstalledVersion = "Not connected";
            Logger.Info("Xbox not connected — skipping installed check");
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
                Logger.Info($"Found installed: {item.Name} v{match.Version}");
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
            InstallComplete = false;
            InstallSuccess = false;
            InstallProgress = 0;
            InstallStatus = null;
            InstallResultMessage = null;
            InstalledVersion = null;
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
        Logger.Info("Loading catalog from Emulation Revival...");

        try
        {
            Logger.Debug("FetchCatalogAsync start");
            _allItems = await _erService.FetchCatalogAsync(forceRefresh: false);
            Logger.Info($"Catalog loaded: {_allItems.Count} items total");

            foreach (var item in _allItems)
                Logger.Info($"  {item.Name}");

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
        var url = SelectedItem?.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.Warn("VisitSite called but no URL");
            return;
        }
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
        if (!_xboxService.IsConnected)
        {
            Logger.Info("Xbox not connected — cannot install");
            InstallComplete = true;
            InstallSuccess = false;
            InstallResultMessage = "Not connected. Connect via sidebar first.";
            return;
        }

        var itemName = SelectedItem?.Name ?? "?";
        var itemUrl = SelectedItem?.DownloadUrl ?? "?";

        IsInstalling = true;
        InstallComplete = false;
        InstallResultMessage = null;
        InstallProgress = 0;
        PackageProgress = 0;
        InstallStatus = "";
        PackageStatus = "";
        CurrentFile = "";
        Logger.Info($"Install starting: {itemName} from {itemUrl}");

        var progress = new Progress<InstallProgressInfo>(info =>
        {
            InstallProgress = info.Total;
            PackageProgress = info.File;
            PackageStatus = info.Status;
            CurrentFile = info.CurrentFile;

            InstallStatus = info.Status;
        });

        Logger.Debug("Calling DownloadAndInstallAsync");
        var result = await _installService.DownloadAndInstallAsync(SelectedItem!, progress);

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
                    await Task.Delay(3000);
#endif
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
