using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class CrashDataViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public CrashDataViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        CrashDumps.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasCrashDumps));
        };
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _crashDumpEnabled;

    public ObservableCollection<CrashDumpInfo> CrashDumps { get; } = [];

    [ObservableProperty]
    private CrashDumpInfo? _selectedCrashDump;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrashDumpSelected))]
    private bool _isRefreshing;

    public bool IsCrashDumpSelected => SelectedCrashDump is not null;
    public bool HasCrashDumps => CrashDumps.Count > 0;

    public void Initialize()
    {
        if (!_xboxService.IsConnected) return;
        _ = LoadAllAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        StatusMessage = null;
        await LoadAllAsync();
        IsRefreshing = false;
    }

    private async Task LoadAllAsync()
    {
        StatusMessage = null;

        try
        {
            var controlJson = await _xboxService.GetCrashControlAsync();
            if (controlJson is not null)
            {
                try
                {
                    var control = JsonSerializer.Deserialize<CrashControlInfo>(controlJson);
                    CrashDumpEnabled = control?.CrashDumpEnabled ?? false;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to parse crash control info: {ex.Message}");
                    CrashDumpEnabled = false;
                }
            }

            var json = await _xboxService.GetCrashDumpsAsync();
            if (json is null)
            {
                StatusMessage = "Failed to get crash dumps (API may not be available on this console)";
                CrashDumps.Clear();
                return;
            }

            var resp = JsonSerializer.Deserialize<CrashDumpListResponse>(json);
            if (resp?.CrashDumps is null || resp.CrashDumps.Count == 0)
            {
                CrashDumps.Clear();
                StatusMessage = "No crash dumps found";
                return;
            }

            CrashDumps.Clear();
            foreach (var d in resp.CrashDumps.OrderByDescending(d => d.CreatedAt))
                CrashDumps.Add(d);
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load crash data";
            Logger.Error(ex, "LoadCrashData failed");
        }
    }

    [RelayCommand]
    private async Task ToggleCrashDumpAsync()
    {
        try
        {
            var ok = await _xboxService.SetCrashControlAsync(!CrashDumpEnabled);
            if (ok)
            {
                CrashDumpEnabled = !CrashDumpEnabled;
                StatusMessage = CrashDumpEnabled
                    ? "Crash dumps enabled"
                    : "Crash dumps disabled";
            }
            else
            {
                StatusMessage = "Failed to toggle crash dump setting";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error toggling crash dump setting";
            Logger.Error(ex, "ToggleCrashDump failed");
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCrashDump is null) return;

        var name = SelectedCrashDump.FileName ?? "unknown";

        try
        {
            var ok = await _xboxService.DeleteCrashDumpAsync(name);
            if (ok)
            {
                CrashDumps.Remove(SelectedCrashDump);
                SelectedCrashDump = null;
                StatusMessage = $"Deleted: {name}";
            }
            else
            {
                StatusMessage = $"Failed to delete: {name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting {name}";
            Logger.Error(ex, $"DeleteCrashDump failed for {name}");
        }
    }

    [RelayCommand]
    private async Task DeleteAllAsync()
    {
        CrashDumpInfo[] all;
        lock (CrashDumps)
        {
            all = CrashDumps.ToArray();
        }

        var deleted = 0;
        foreach (var d in all)
        {
            var name = d.FileName ?? "unknown";
            try
            {
                var ok = await _xboxService.DeleteCrashDumpAsync(name);
                if (ok)
                {
                    CrashDumps.Remove(d);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"DeleteCrashDump failed for {name}");
            }
        }

        SelectedCrashDump = null;
        StatusMessage = deleted > 0
            ? $"Deleted {deleted} crash dump(s)"
            : "No crash dumps deleted";
    }
}