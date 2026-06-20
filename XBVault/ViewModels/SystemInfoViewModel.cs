using System;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class SystemInfoViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public SystemInfoViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    [ObservableProperty]
    private bool _isLoading;

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

            SystemInfoText =
                $"Console Type:   {info.ConsoleType ?? "-"}\n" +
                $"OS Version:     {info.OsVersion ?? "-"}\n" +
                $"OS Edition:     {info.OsEdition ?? "-"}\n" +
                $"Device Name:    {info.DeviceName ?? "-"}\n" +
                $"Platform:       {info.Platform ?? "-"}\n" +
                $"Region:         {info.Region ?? "-"}\n" +
                $"Language:       {info.Language ?? "-"}";
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
}
