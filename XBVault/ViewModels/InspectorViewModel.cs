using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class InspectorViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public InspectorViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        _xboxService.ConnectionChanged += OnConnectionChanged;
        IsConnected = _xboxService.IsConnected;
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
    }

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasSessions;

    [ObservableProperty]
    private string? _scannedHost;

    public bool ShowDisconnected => !IsConnected;
    public bool ShowContent => IsConnected;
    public bool ShowScanReady => IsConnected && !IsScanning;
    public bool ShowScanning => IsScanning;
    public bool ShowSessions => HasSessions;
    public bool ShowEmptySessions => IsConnected && !HasSessions && !IsScanning;

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisconnected));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowScanReady));
        if (!value)
        {
            IsScanning = false;
            HasSessions = false;
            ScannedHost = null;
        }
    }

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowScanReady));
        OnPropertyChanged(nameof(ShowScanning));
        OnPropertyChanged(nameof(ShowEmptySessions));
    }

    partial void OnHasSessionsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSessions));
        OnPropertyChanged(nameof(ShowEmptySessions));
    }

    public Func<Task<bool>>? ShowConnectAction { get; set; }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (ShowConnectAction is not null)
        {
            var ok = await ShowConnectAction();
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (!_xboxService.IsConnected)
        {
            StatusMessage = "Not connected. Connect via sidebar first.";
            return;
        }

        IsScanning = true;
        StatusMessage = null;
        ScannedHost = SettingsService.Current.XboxConnection.Address;
        HasSessions = false;

        try
        {
            // TODO: implement scan logic in future change
            await Task.Delay(500);
            StatusMessage = "Scan complete — no agents found. Inspector scan not yet implemented.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Inspector scan failed");
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void ClearStatus()
    {
        StatusMessage = null;
    }
}
