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
        Logger.Debug("InstalledViewModel initialized");
    }

    public ObservableCollection<InstalledPackage> Packages { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasPackages;

    [ObservableProperty]
    private string? _statusMessage;

    public bool ShowGrid => HasPackages && !IsLoading;
    public bool ShowRefreshPrompt => !IsLoading && !HasPackages && string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowRefreshPrompt));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRefreshPrompt));
        OnPropertyChanged(nameof(ShowGrid));
    }

    partial void OnHasPackagesChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRefreshPrompt));
        OnPropertyChanged(nameof(ShowGrid));
    }

    [ObservableProperty]
    private InstalledPackage? _selectedPackage;

    public bool IsPackageSelected => SelectedPackage is not null;

    partial void OnSelectedPackageChanged(InstalledPackage? value)
    {
        OnPropertyChanged(nameof(IsPackageSelected));
        if (value is not null)
        {
            Logger.Info($"Selected package raw:\n{value.RawJson}");
        }
    }

    [RelayCommand]
    private async Task RefreshPackagesAsync()
    {
        if (!_xboxService.IsConnected)
        {
            Logger.Info("Xbox not connected — skipping package refresh");
            StatusMessage = "Not connected. Connect via sidebar first.";
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        Logger.Info("Refreshing installed packages...");

        try
        {
            var packages = await _xboxService.GetInstalledPackagesAsync();
            Packages.Clear();

            var filtered = packages
                .Where(p => p.Publisher is null ||
                            !p.Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Logger.Info($"Total packages from Xbox: {packages.Count}, after system filter: {filtered.Count}");

            foreach (var pkg in packages.OrderBy(p => p.Name))
            {
                var isSystem = pkg.Publisher is not null &&
                               pkg.Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
                if (isSystem)
                {
                    Logger.Debug($"  [SYSTEM] {pkg.Name,-30} v{pkg.Version}  {pkg.PackageFamilyName ?? ""}");
                    continue;
                }
                Packages.Add(pkg);
                Logger.Info($"  {pkg.Name,-30} v{pkg.Version,-14}  {pkg.DisplayPublisher ?? "-",-20}  {pkg.PackageFamilyName ?? ""}");
            }

            HasPackages = Packages.Count > 0;
            Logger.Info($"User-installed packages shown: {Packages.Count}");

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
