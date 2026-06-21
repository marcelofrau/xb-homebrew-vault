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
    private bool _isPolling;

    [ObservableProperty]
    private string? _lastUpdated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProcessSelected))]
    private ProcessInfo? _selectedProcess;

    public bool IsProcessSelected => SelectedProcess is not null;

    public ObservableCollection<ProcessInfo> Processes { get; } = [];

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _filterText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortPidIndicator))]
    [NotifyPropertyChangedFor(nameof(SortImageNameIndicator))]
    [NotifyPropertyChangedFor(nameof(SortMemoryIndicator))]
    [NotifyPropertyChangedFor(nameof(SortCpuIndicator))]
    [NotifyPropertyChangedFor(nameof(SortUserIndicator))]
    private ProcessSortColumn _sortColumn = ProcessSortColumn.ImageName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortPidIndicator))]
    [NotifyPropertyChangedFor(nameof(SortImageNameIndicator))]
    [NotifyPropertyChangedFor(nameof(SortMemoryIndicator))]
    [NotifyPropertyChangedFor(nameof(SortCpuIndicator))]
    [NotifyPropertyChangedFor(nameof(SortUserIndicator))]
    private bool _sortAscending = true;

    private string SortArrow(bool forColumn) =>
        forColumn ? (SortAscending ? " ▲" : " ▼") : "";

    public string SortPidIndicator => SortColumn == ProcessSortColumn.ProcessId ? SortArrow(true) : "";
    public string SortImageNameIndicator => SortColumn == ProcessSortColumn.ImageName ? SortArrow(true) : "";
    public string SortMemoryIndicator => SortColumn == ProcessSortColumn.MemoryUsage ? SortArrow(true) : "";
    public string SortCpuIndicator => SortColumn == ProcessSortColumn.CpuUsage ? SortArrow(true) : "";
    public string SortUserIndicator => SortColumn == ProcessSortColumn.UserName ? SortArrow(true) : "";

    partial void OnSortColumnChanged(ProcessSortColumn value) => ApplyFilter();
    partial void OnSortAscendingChanged(bool value) => ApplyFilter();

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    [RelayCommand]
    private void SortBy(string? columnName)
    {
        if (Enum.TryParse<ProcessSortColumn>(columnName, out var col))
        {
            if (SortColumn == col)
                SortAscending = !SortAscending;
            else
            {
                SortColumn = col;
                SortAscending = true;
            }
        }
    }

    private void ApplyFilter()
    {
        Processes.Clear();
        var source = _allProcesses;
        if (source is null) return;

        var filtered = source
            .Where(p => string.IsNullOrWhiteSpace(FilterText) ||
                       p.ImageName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) == true ||
                       p.ProcessId.ToString().Contains(FilterText));

        filtered = SortColumn switch
        {
            ProcessSortColumn.ProcessId => SortAscending
                ? filtered.OrderBy(p => p.ProcessId)
                : filtered.OrderByDescending(p => p.ProcessId),
            ProcessSortColumn.ImageName => SortAscending
                ? filtered.OrderBy(p => p.ImageName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(p => p.ImageName, StringComparer.OrdinalIgnoreCase),
            ProcessSortColumn.MemoryUsage => SortAscending
                ? filtered.OrderBy(p => p.MemoryUsage)
                : filtered.OrderByDescending(p => p.MemoryUsage),
            ProcessSortColumn.CpuUsage => SortAscending
                ? filtered.OrderBy(p => p.CpuUsage)
                : filtered.OrderByDescending(p => p.CpuUsage),
            ProcessSortColumn.UserName => SortAscending
                ? filtered.OrderBy(p => p.UserName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(p => p.UserName, StringComparer.OrdinalIgnoreCase),
            _ => filtered
        };

        foreach (var p in filtered)
            Processes.Add(p);
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
            LastUpdated = DateTime.Now.ToString("HH:mm:ss");
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
