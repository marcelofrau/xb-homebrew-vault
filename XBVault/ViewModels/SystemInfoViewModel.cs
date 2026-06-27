using System;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;
using Avalonia.Input;

namespace XBVault.ViewModels;

public partial class SystemInfoViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public SystemInfoViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    public void Initialize()
    {
        if (!_xboxService.IsConnected) return;
        _ = RefreshAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isLoading;

    public Cursor? Cursor => IsLoading ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    [ObservableProperty]
    private string? _systemInfoText;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            var json = await _xboxService.GetSystemInfoAsync();
            if (json is null)
            {
                StatusMessage = "Failed to get system info";
                return;
            }

            var info = JsonSerializer.Deserialize<SystemInfo>(json);
            if (info is null)
            {
                StatusMessage = "Failed to parse system info";
                return;
            }

            SystemInfoText = FormatSystemInfo(info);
        }
        catch (Exception ex)
        {
            StatusMessage = "System info failed";
            Logger.Error(ex, "RefreshSystemInfo failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatSystemInfo(SystemInfo info)
    {
        var sb = new System.Text.StringBuilder();

        void Add(string label, string? val)
        {
            if (val is not null)
                sb.AppendLine($"{label,-22}{val}");
        }

        Add("Console Type:",         info.ConsoleType);
        Add("OS Version:",           info.OsVersion);
        Add("OS Edition:",           info.OsEdition);
        Add("Device Name:",          info.DeviceName);
        Add("Platform:",             info.Platform);
        Add("Region:",               info.Region);
        Add("Language:",             info.Language);
        Add("Serial Number:",        info.SerialNumber);
        Add("Xbox Live Key:",        info.XboxLiveDeviceKey);
        Add("Total Memory:",         info.TotalMemoryDisplay);
        Add("CPU:",                  info.Cpu);
        Add("System Uptime:",        info.SystemUptimeDisplay);
        Add("MAC Address:",          info.MacAddress);
        Add("Firmware Version:",     info.FirmwareVersion);
        Add("Hardware Version:",     info.XboxHardwareVersion);

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "No system info available";
    }
}
