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
        if (item is null) return;

        IsCheckingInstalled = true;
        InstalledVersion = null;

        if (!_xboxService.IsConfigured)
        {
            InstalledVersion = "Not connected";
            IsCheckingInstalled = false;
            return;
        }

        try
        {
            var packages = await _xboxService.GetInstalledPackagesAsync();
            var match = packages.FirstOrDefault(p =>
                p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            InstalledVersion = match?.Version ?? "Not installed";
        }
        catch
        {
            InstalledVersion = "Check failed";
        }
        finally
        {
            IsCheckingInstalled = false;
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
            ShowDetailAction?.Invoke(value);
        }
    }

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        IsLoading = true;

        try
        {
            _allItems = await _erService.FetchCatalogAsync();
            ApplyFilters();
            _ = LoadThumbnailsAsync();
        }
        finally
        {
            IsLoading = false;
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
        if (SelectedItem is null || IsInstalling) return;

        IsInstalling = true;
        InstallProgress = 0;
        InstallStatus = "Downloading...";

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

        var result = await _installService.DownloadAndInstallAsync(SelectedItem, progress);

        if (!result)
            InstallStatus = "Install failed";

        InstallProgress = result ? 1.0 : 0;
        IsInstalling = false;
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
    }

    private async Task LoadThumbnailsAsync()
    {
        foreach (var item in _allItems)
        {
            if (string.IsNullOrEmpty(item.ImageUrl) || item.Thumbnail is not null)
                continue;

            try
            {
                var bytes = await ImageHttp.GetByteArrayAsync(item.ImageUrl);
                using var ms = new MemoryStream(bytes);
                item.Thumbnail = new Bitmap(ms);

                var idx = Items.IndexOf(item);
                if (idx >= 0)
                {
                    Items.RemoveAt(idx);
                    Items.Insert(idx, item);
                }
            }
            catch
            {
            }
        }
    }
}
