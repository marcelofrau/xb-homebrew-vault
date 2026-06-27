using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class PerformanceViewModel : ObservableObject, IDisposable
{
    private readonly XboxDeviceService _xboxService;
    private CancellationTokenSource? _cts;

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    public PerformanceViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private string _currentMemoryDetail = "0 MB / 0 MB";

    [ObservableProperty]
    private string _lastUpdate = "";

    public Views.PerformanceChart CpuChart { get; } = new() { Title = "CPU", Stroke = Brushes.Lime };
    public Views.PerformanceChart GpuChart { get; } = new() { Title = "GPU", Stroke = Brushes.Cyan };
    public Views.PerformanceChart MemoryChart { get; } = new() { Title = "MEMORY", Stroke = Brushes.Orange };
    public Views.PerformanceChart IoChart { get; } = new() { Title = "I/O", Stroke = Brushes.Magenta };

    [RelayCommand]
    private void StartMonitoring()
    {
        if (IsMonitoring) return;
        if (!_xboxService.IsConnected) return;

        IsMonitoring = true;
        _cts = new CancellationTokenSource();

        CpuChart.Clear();
        GpuChart.Clear();
        MemoryChart.Clear();
        IoChart.Clear();

        _ = Task.Run(() => _xboxService.ConnectPerformanceWsAsync(OnSnapshot, _cts.Token));
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsMonitoring = false;
    }

    private void OnSnapshot(PerformanceSnapshot snap)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CpuChart.AddValue(snap.CpuLoad);
            CpuChart.CurrentValue = $"{snap.CpuLoad:F0}%";

            GpuChart.AddValue(snap.GpuUsage);
            GpuChart.CurrentValue = $"{snap.GpuUsage:F1}%";

            MemoryChart.AddValue(snap.MemoryPercent);
            MemoryChart.CurrentValue = $"{snap.MemoryPercent:F0}%";
            CurrentMemoryDetail = $"{snap.MemoryUsedMB:F0} MB / {snap.MemoryTotalMB:F0} MB";

            var io = snap.IoTotalSpeed;
            var ioStr = io >= 1_000_000 ? $"{io / 1_000_000.0:F2} MB/s" :
                         io >= 1_000 ? $"{io / 1_000.0:F1} KB/s" :
                         $"{io} B/s";
            IoChart.AddValue(io / 1_000_000.0);
            IoChart.CurrentValue = ioStr;

            LastUpdate = DateTime.Now.ToString("HH:mm:ss");

            Logger.Info($"Perf: CPU={snap.CpuLoad:F1}% GPU={snap.GpuUsage:F1}% " +
                        $"Mem={snap.MemoryPercent:F0}% IO={ioStr}");
        });
    }
}
