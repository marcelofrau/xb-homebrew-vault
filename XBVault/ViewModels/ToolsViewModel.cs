#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

/// <summary>
/// Exposes tool-launch commands and connection-aware visibility state for the Tools surface.
/// </summary>
/// <remarks>
/// Tool actions are delegates supplied by the shell so desktop and Android can present different windows,
/// routes, or modal flows while sharing the same command surface.
/// </remarks>
public partial class ToolsViewModel : ObservableObject
{
    private readonly IXboxAuthService _authService;
    private readonly IXboxSystemService _systemService;
    private readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public ToolsViewModel(IXboxAuthService authService, IXboxSystemService systemService)
    {
        _authService = authService;
        _systemService = systemService;
        _authService.ConnectionChanged += OnConnectionChanged;
        IsConnected = _authService.IsConnected;
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        if (await _authService.EnsureConnectedAsync())
            return true;
        StatusMessage = "Not connected. Connect via sidebar first.";
        return false;
    }

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string? _statusMessage;

    public bool ShowDisconnected => !IsConnected;
    public bool ShowContent => IsConnected;
    public bool IsWindows => _isWindows;

    public Action<string>? OpenUrlAction { get; set; }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisconnected));
        OnPropertyChanged(nameof(ShowContent));
    }

    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Action? ShowScreenshotAction { get; set; }
    public Action? ShowSystemInfoAction { get; set; }
    public Action? ShowProcessesAction { get; set; }
    public Action? ShowNetworkInfoAction { get; set; }
    public Action? ShowPerformanceAction { get; set; }
    public Action? ShowCustomInstallAction { get; set; }
    public Action? ShowCrashDataAction { get; set; }
    public Action? ShowUsbPermissionAction { get; set; }
    public Action? OpenLoopbackExemptAction { get; set; }
    public Action? OpenLoopbackExemptQuickAction { get; set; }
    public Func<string, string, string, Task>? ShowInfoAsync { get; set; }
    public Func<string, string, string, string, string?, string?, Task<bool>>? ShowConfirmAsync { get; set; }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (ShowConnectAction is not null)
        {
            var ok = await ShowConnectAction();
            if (ok)
                _authService.MarkConnected();
        }
    }

    [RelayCommand]
    private async Task OpenScreenshotAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        ShowScreenshotAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenSystemInfoAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        ShowSystemInfoAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenProcessesAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        ShowProcessesAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenNetworkInfoAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        ShowNetworkInfoAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenPerformanceAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        ShowPerformanceAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenCustomInstallAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        ShowCustomInstallAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenCrashDataAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        ShowCrashDataAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenUsbPermissionAsync()
    {
        if (!IsWindows)
        {
            if (ShowInfoAsync is not null)
            {
                await ShowInfoAsync(
                    "Windows Only",
                    "USB Media Drive activation is currently only available on Windows.",
                    "We're evaluating support for Linux and macOS. Stay tuned for future updates!");
            }
            return;
        }
        ShowUsbPermissionAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenLoopbackExemptAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        OpenLoopbackExemptAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenLoopbackExemptQuickAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        OpenLoopbackExemptQuickAction?.Invoke();
    }

    [RelayCommand]
    private async Task OpenDevPortalAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        var url = _authService.GetDevPortalUrl();
        if (string.IsNullOrEmpty(url))
        {
            StatusMessage = "No Xbox URL configured";
            return;
        }
        try
        {
            if (OpenUrlAction is not null)
                OpenUrlAction(url);
            else
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to open browser";
            Logger.Error(ex, "OpenDevPortal failed");
        }
    }

    [RelayCommand]
    private async Task RestartXboxAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        if (ShowConfirmAsync is not null)
        {
            var ok = await ShowConfirmAsync("Restart Xbox", "Are you sure you want to restart the Xbox? This will disconnect you.", "Restart", "Cancel", null, "avares://XBVault/Assets/Views/ErrorDialog/errordialog-restart-48.png");
            if (!ok) return;
        }
        StatusMessage = "Restarting Xbox...";
        try
        {
            var ok = await _systemService.RestartXboxAsync();
            StatusMessage = ok ? "Restart command sent" : "Restart failed";
        }
        catch (Exception ex)
        {
            StatusMessage = "Restart failed";
            Logger.Error(ex, "Restart failed");
        }
    }

    [RelayCommand]
    private async Task ShutdownXboxAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        if (ShowConfirmAsync is not null)
        {
            var ok = await ShowConfirmAsync("Shutdown Xbox", "Are you sure you want to shutdown the Xbox? This will disconnect you.", "Shutdown", "Cancel", null, "avares://XBVault/Assets/Views/ErrorDialog/errordialog-shutdown-48.png");
            if (!ok) return;
        }
        StatusMessage = "Shutting down Xbox...";
        try
        {
            var ok = await _systemService.ShutdownXboxAsync();
            StatusMessage = ok ? "Shutdown command sent" : "Shutdown failed";
        }
        catch (Exception ex)
        {
            StatusMessage = "Shutdown failed";
            Logger.Error(ex, "Shutdown failed");
        }
    }
}
