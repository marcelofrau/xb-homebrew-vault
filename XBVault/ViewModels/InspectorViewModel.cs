using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class InspectorViewModel : ObservableObject
{
    private const double DefaultConsoleFontSize = 13;
    private const double MinConsoleFontSize = 10;
    private const double MaxConsoleFontSize = 24;

    private readonly XboxDeviceService _xboxService;

    public InspectorViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        _xboxService.ConnectionChanged += OnConnectionChanged;
        ConsoleFontSize = ClampConsoleFontSize(SettingsService.Current.ConsoleFontSize);
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

    [ObservableProperty]
    private string? _selectedSession;

    [ObservableProperty]
    private string _replInput = "";

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseConsoleFontSizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseConsoleFontSizeCommand))]
    private double _consoleFontSize;

    public bool ShowDisconnected => !IsConnected;
    public bool ShowContent => IsConnected;
    public bool ShowScanReady => IsConnected && !IsScanning;
    public bool ShowScanning => IsScanning;
    public bool ShowSessions => HasSessions;
    public bool ShowEmptySessions => IsConnected && !HasSessions && !IsScanning;
    public bool ShowNoSessionsFound => IsConnected && !IsScanning && !HasSessions;

    public ObservableCollection<string> ConsoleEntries { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ScannedSessions { get; } = new ObservableCollection<string>();

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisconnected));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowScanReady));
        OnPropertyChanged(nameof(ShowNoSessionsFound));
        if (!value)
        {
            IsScanning = false;
            HasSessions = false;
            ScannedSessions.Clear();
            SelectedSession = null;
            ScannedHost = null;
        }
    }

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowScanReady));
        OnPropertyChanged(nameof(ShowScanning));
        OnPropertyChanged(nameof(ShowEmptySessions));
        OnPropertyChanged(nameof(ShowNoSessionsFound));
    }

    partial void OnHasSessionsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSessions));
        OnPropertyChanged(nameof(ShowEmptySessions));
        OnPropertyChanged(nameof(ShowNoSessionsFound));
    }

    partial void OnConsoleFontSizeChanged(double value)
    {
        var clamped = ClampConsoleFontSize(value);
        if (Math.Abs(clamped - value) > double.Epsilon)
        {
            ConsoleFontSize = clamped;
            return;
        }
        SettingsService.Current.ConsoleFontSize = clamped;
        SettingsService.Save();
    }

    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Action? ShowGuideAction { get; set; }

    [RelayCommand]
    private void OpenGuide()
    {
        ShowGuideAction?.Invoke();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (ShowConnectAction is not null)
        {
            var ok = await ShowConnectAction();
            if (ok)
                _xboxService.MarkConnected();
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
            ConsoleEntries.Add($"[{DateTime.Now:HH:mm:ss}] Scanning ports 9000-9010 on {ScannedHost}...");
            await Task.Delay(500);
            ConsoleEntries.Add($"[{DateTime.Now:HH:mm:ss}] Scan complete — no agents found.");
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

    [RelayCommand]
    private void ClearConsole()
    {
        ConsoleEntries.Clear();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var input = ReplInput?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        ReplInput = "";
        ConsoleEntries.Add($"[{DateTime.Now:HH:mm:ss}] > {input}");

        if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleEntries.Clear();
            return;
        }

        // TODO: forward command to selected agent session
        ConsoleEntries.Add($"[{DateTime.Now:HH:mm:ss}] No active session — command not sent.");
    }

    [RelayCommand]
    private void ShowHelp()
    {
        ConsoleEntries.Add("");
        ConsoleEntries.Add("=== INSPECTOR REPL HELP ===");
        ConsoleEntries.Add("  help              Show this help");
        ConsoleEntries.Add("  clear             Clear console");
        ConsoleEntries.Add("  scan              Run agent discovery scan");
        ConsoleEntries.Add("  connect           Open connection dialog");
        ConsoleEntries.Add("  status            Show connection info");
        ConsoleEntries.Add("  <command>         Send raw command to selected agent");
        ConsoleEntries.Add("");
        ConsoleEntries.Add("Select a discovered agent from the channel list to");
        ConsoleEntries.Add("target commands to that agent.");
        ConsoleEntries.Add("");
        ConsoleEntries.Add("  guide             Open Inspector documentation page");
        ConsoleEntries.Add("");
        ConsoleEntries.Add("⚠ Under development & testing — scan and REPL are stubs.");
        ConsoleEntries.Add("==========================");
        ConsoleEntries.Add("");
    }

    [RelayCommand(CanExecute = nameof(CanIncreaseConsoleFontSize))]
    private void IncreaseConsoleFontSize()
    {
        ConsoleFontSize += 1;
    }

    private bool CanIncreaseConsoleFontSize() => ConsoleFontSize < MaxConsoleFontSize;

    [RelayCommand(CanExecute = nameof(CanDecreaseConsoleFontSize))]
    private void DecreaseConsoleFontSize()
    {
        ConsoleFontSize -= 1;
    }

    private bool CanDecreaseConsoleFontSize() => ConsoleFontSize > MinConsoleFontSize;

    private static double ClampConsoleFontSize(double value)
    {
        if (double.IsNaN(value) || value <= 0)
            return DefaultConsoleFontSize;
        return Math.Clamp(value, MinConsoleFontSize, MaxConsoleFontSize);
    }
}
