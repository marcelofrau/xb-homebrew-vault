using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    private readonly EmulationRevivalService _erService;
    private List<CatalogItem> _allItems = [];

    public BrowseViewModel(EmulationRevivalService erService)
    {
        _erService = erService;
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
    private bool _showExperimental = true;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private CatalogItem? _selectedItem;

    [ObservableProperty]
    private bool _isDetailVisible;

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnShowExperimentalChanged(bool value) => ApplyFilters();

    partial void OnSelectedItemChanged(CatalogItem? value)
    {
        IsDetailVisible = value is not null;
    }

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        IsLoading = true;

        try
        {
            _allItems = await _erService.FetchCatalogAsync();
            ApplyFilters();
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
    private void BackToGrid()
    {
        SelectedItem = null;
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
}
