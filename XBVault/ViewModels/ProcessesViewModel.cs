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

public partial class ProcessesViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private ProcessInfo[]? _allProcesses;

    public ProcessesViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProcessSelected))]
    private ProcessInfo? _selectedProcess;

    public bool IsProcessSelected => SelectedProcess is not null;

    public ObservableCollection<ProcessInfo> Processes { get; } = [];

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _filterText;

    partial void OnFilterTextChanged(string? value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Processes.Clear();
        var source = _allProcesses;
        if (source is null) return;

        foreach (var p in source)
        {
            if (string.IsNullOrWhiteSpace(FilterText) ||
                p.ImageName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) == true ||
                p.ProcessId.ToString().Contains(FilterText))
            {
                Processes.Add(p);
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            var json = await _xboxService.GetProcessesAsync();
            if (json is null)
            {
                StatusMessage = "Failed to get process list";
                return;
            }

            var resp = JsonSerializer.Deserialize<ProcessListResponse>(json);
            if (resp?.Processes is null)
            {
                StatusMessage = "Failed to parse process list";
                return;
            }

            _allProcesses = [.. resp.Processes];
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = "Process list failed";
            Logger.Error(ex, "RefreshProcesses failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task KillSelectedAsync()
    {
        if (SelectedProcess is null) return;

        var pid = SelectedProcess.ProcessId;
        var name = SelectedProcess.ImageName ?? $"PID {pid}";

        try
        {
            var ok = await _xboxService.KillProcessAsync(pid);
            StatusMessage = ok ? $"Killed: {name}" : $"Failed to kill: {name}";
            if (ok)
            {
                _allProcesses = _allProcesses?.Where(p => p.ProcessId != pid).ToArray();
                ApplyFilter();
                SelectedProcess = null;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error killing {name}";
            Logger.Error(ex, $"KillProcess failed for {name}");
        }
    }
}
