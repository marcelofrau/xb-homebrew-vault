using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class InstalledViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public InstalledViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    public ObservableCollection<InstalledPackage> Packages { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasPackages;

    [ObservableProperty]
    private bool _isUninstalling;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private InstalledPackage? _selectedPackage;

    public bool IsPackageSelected => SelectedPackage is not null;

    partial void OnSelectedPackageChanged(InstalledPackage? value)
    {
        OnPropertyChanged(nameof(IsPackageSelected));
    }

    [RelayCommand]
    private async Task RefreshPackagesAsync()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            var packages = await _xboxService.GetInstalledPackagesAsync();
            Packages.Clear();

            foreach (var pkg in packages.OrderBy(p => p.Name))
                Packages.Add(pkg);

            HasPackages = Packages.Count > 0;

            if (!HasPackages)
                StatusMessage = "No packages installed or Xbox not connected";
        }
        catch
        {
            StatusMessage = "Failed to load packages";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        if (SelectedPackage is null) return;

        IsUninstalling = true;
        StatusMessage = $"Uninstalling {SelectedPackage.Name}...";

        try
        {
            var result = await _xboxService.UninstallPackageAsync(SelectedPackage.FullName);
            StatusMessage = result
                ? $"{SelectedPackage.Name} uninstalled"
                : $"Failed to uninstall {SelectedPackage.Name}";

            await RefreshPackagesAsync();
        }
        catch
        {
            StatusMessage = "Uninstall failed";
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

        try
        {
            var result = await _xboxService.InstallPackageAsync(filePath);
            StatusMessage = result ? "Install complete" : "Install failed";

            await RefreshPackagesAsync();
        }
        catch
        {
            StatusMessage = "Install failed";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
