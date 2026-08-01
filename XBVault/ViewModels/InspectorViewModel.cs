using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class InspectorViewModel : ObservableObject
{
    private const double DefaultConsoleFontSize = 11;
    private const double MinConsoleFontSize = 10;
    private const double MaxConsoleFontSize = 24;

    private static readonly IBrush _defaultBrush = new SolidColorBrush(0xFFF0F0F0);
    private static readonly IBrush _mutedBrush = new SolidColorBrush(0xFF8B8D91);
    private static readonly IBrush _errorBrush = new SolidColorBrush(0xFFE74C3C);
    private static readonly IBrush _successBrush = new SolidColorBrush(0xFF2ECC71);
    private static readonly IBrush _warnBrush = new SolidColorBrush(0xFFF39C12);
    private static readonly IBrush _accentBrush = new SolidColorBrush(0xFF9ACA3C);

    private readonly IXboxAuthService _authService;
    private readonly XrayAgentService _agentService;

    public InspectorViewModel(IXboxAuthService authService, XrayAgentService? agentService = null)
    {
        _authService = authService;
        _agentService = agentService ?? new XrayAgentService();
        _authService.ConnectionChanged += OnConnectionChanged;
        ConsoleFontSize = ClampConsoleFontSize(SettingsService.Current.ConsoleFontSize);
        IsConnected = _authService.IsConnected;
        SubscribeAgentEvents();
    }

    private void SubscribeAgentEvents()
    {
        _agentService.LogReceived += OnAgentLog;
        _agentService.ReplResultReceived += OnAgentReplResult;
        _agentService.CommandResultReceived += OnAgentCommandResult;
        _agentService.Disconnected += OnAgentDisconnected;
    }

    private void UnsubscribeAgentEvents()
    {
        _agentService.LogReceived -= OnAgentLog;
        _agentService.ReplResultReceived -= OnAgentReplResult;
        _agentService.CommandResultReceived -= OnAgentCommandResult;
        _agentService.Disconnected -= OnAgentDisconnected;
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
    }

    private void OnAgentLog(XrayLogMessage msg)
    {
        var level = msg.Payload?.Level ?? "INFO";
        var text = msg.Payload?.Message ?? "";
        var tag = msg.Payload?.Tag;
        var ts = msg.Payload?.Timestamp ?? "";
        var prefix = string.IsNullOrEmpty(ts) ? $"[{level}]" : $"[{ts}][{level}]";
        if (!string.IsNullOrEmpty(tag))
            prefix += $"[{tag}]";

        var entry = new InspectorConsoleEntry { Text = $"{prefix} {text}" };
        entry.Foreground = level switch
        {
            "ERROR" or "FATAL" => _errorBrush,
            "WARN" => _warnBrush,
            "DEBUG" or "TRACE" => _mutedBrush,
            "SUCCESS" => _successBrush,
            _ => _defaultBrush
        };

        Dispatcher.UIThread.Post(() => ConsoleEntries.Add(entry));
    }

    private void OnAgentReplResult(XrayReplResult result)
    {
        var p = result.Payload;
        if (p is null) return;

        Dispatcher.UIThread.Post(() =>
        {
            LogMuted($"[{DateTime.Now:HH:mm:ss}] <<< REPL result (succ={p.Success})");
            if (p.Success && !string.IsNullOrEmpty(p.Output))
                Log($"[REPL] {p.Output}");
            else if (!p.Success)
                LogError($"[REPL] Error: {p.Error ?? "unknown"}");
        });
    }

    private void OnAgentCommandResult(XrayCommandResult result)
    {
        var p = result.Payload;
        if (p is null) return;

        Dispatcher.UIThread.Post(() =>
        {
            LogMuted($"[{DateTime.Now:HH:mm:ss}] <<< cmd result ({p.Command} succ={p.Success})");
            if (p.Success)
                LogSuccess($"[{p.Command}] {p.Message}");
            else
                LogError($"[{p.Command}] Failed: {p.Message}");
        });
    }

    private void OnAgentDisconnected(string reason)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogWarn($"Agent disconnected: {reason}");
            if (SelectedSession is not null)
                SelectedSession.IsConnected = false;
            IsAgentConnected = false;
            SelectedSession = null;
        });
    }

    private void Log(string text) => ConsoleEntries.Add(new InspectorConsoleEntry { Text = text, Foreground = _defaultBrush });
    private void LogMuted(string text) => ConsoleEntries.Add(new InspectorConsoleEntry { Text = text, Foreground = _mutedBrush });
    private void LogError(string text) => ConsoleEntries.Add(new InspectorConsoleEntry { Text = text, Foreground = _errorBrush });
    private void LogSuccess(string text) => ConsoleEntries.Add(new InspectorConsoleEntry { Text = text, Foreground = _successBrush });
    private void LogWarn(string text) => ConsoleEntries.Add(new InspectorConsoleEntry { Text = text, Foreground = _warnBrush });
    private void LogCmd(string text) => ConsoleEntries.Add(new InspectorConsoleEntry { Text = text, Foreground = _accentBrush });

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
    private XrayAgentInfo? _selectedSession;

    [ObservableProperty]
    private bool _isAgentConnected;

    [ObservableProperty]
    private string _replInput = "";

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private bool _isFilterVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterMatchText))]
    private string _filterText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterMatchText))]
    private int _filterMatchCount;

    public string FilterMatchText =>
        string.IsNullOrEmpty(FilterText) ? "" : $"{FilterMatchCount} matches";

    public event Action? FilterChanged;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseConsoleFontSizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseConsoleFontSizeCommand))]
    private double _consoleFontSize;

    public bool ShowDisconnected => !IsConnected && !HasOverride;
    public bool ShowContent => IsConnected || HasOverride;
    public bool ShowScanReady => (IsConnected || HasOverride) && !IsScanning;
    public bool ShowScanning => IsScanning;
    public bool ShowSessions => HasSessions;
    public bool ShowNoSessionsFound => (IsConnected || HasOverride) && !IsScanning && !HasSessions;
    public ObservableCollection<InspectorConsoleEntry> ConsoleEntries { get; } = new();
    public ObservableCollection<XrayAgentInfo> ScannedSessions { get; } = new ObservableCollection<XrayAgentInfo>();

    public Func<string, Task>? OpenCustomInstallWithFileAction { get; set; }

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
            if (IsAgentConnected) DisconnectAgent();
            ScannedSessions.Clear();
            SelectedSession = null;
            ScannedHost = null;
        }
    }

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowScanReady));
        OnPropertyChanged(nameof(ShowScanning));
        OnPropertyChanged(nameof(ShowNoSessionsFound));
    }

    partial void OnHasSessionsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSessions));
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverride))]
    [NotifyPropertyChangedFor(nameof(ShowDisconnected))]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    [NotifyPropertyChangedFor(nameof(ShowScanReady))]
    [NotifyPropertyChangedFor(nameof(ShowNoSessionsFound))]
    private string? _overrideAddress;

    public bool HasOverride => !string.IsNullOrEmpty(OverrideAddress);

    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Action? ShowGuideAction { get; set; }
    public Func<string, string, string, string, string?, string?, Task<bool>>? ShowConfirmAsync { get; set; }
    public Func<string, Task<string?>>? ShowSaveFileDialogAsync { get; set; }
    public Func<string, string, string, string?, Task<string?>>? ShowInputPromptAsync { get; set; }

    partial void OnFilterTextChanged(string value)
    {
        MarkMatches();
        FilterChanged?.Invoke();
    }

    [RelayCommand]
    private void ToggleFilter()
    {
        IsFilterVisible = !IsFilterVisible;
    }

    [RelayCommand]
    private void CloseFilter()
    {
        IsFilterVisible = false;
    }

    private void MarkMatches()
    {
        FilterMatchCount = 0;
        var filter = FilterText?.Trim();

        if (string.IsNullOrEmpty(filter))
        {
            foreach (var e in ConsoleEntries)
                e.IsMatch = false;
            return;
        }

        foreach (var e in ConsoleEntries)
        {
            var match = e.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
            e.IsMatch = match;
            if (match) FilterMatchCount++;
        }
    }

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
                _authService.MarkConnected();
        }
    }

    [RelayCommand]
    private async Task SetOverrideAddressAsync()
    {
        if (ShowInputPromptAsync is null) return;
        var defaultAddr = SettingsService.Current.XboxConnection.Address;
        var result = await ShowInputPromptAsync(
            "IP Override",
            $"Enter Xbox IP address (default: {defaultAddr}):",
            OverrideAddress ?? defaultAddr, null);
        if (result is null) return;
        OverrideAddress = string.IsNullOrWhiteSpace(result) || result.Trim() == defaultAddr
            ? null : result.Trim();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (!_authService.IsConnected && !HasOverride)
        {
            StatusMessage = "Not connected. Connect via sidebar first.";
            return;
        }

        IsScanning = true;
        StatusMessage = null;
        var settings = SettingsService.Current.XboxConnection;
        ScannedHost = OverrideAddress ?? settings.Address;
        HasSessions = false;

        if (IsAgentConnected) DisconnectAgent();

        try
        {
            LogMuted($"[{DateTime.Now:HH:mm:ss}] Scanning ports 9000-9009 on {ScannedHost}...");
            var agents = await _agentService.ScanAsync(ScannedHost);

            ScannedSessions.Clear();
            foreach (var agent in agents)
                ScannedSessions.Add(agent);

            if (agents.Count > 0)
            {
                HasSessions = true;
                SelectedSession = agents[0];
                LogSuccess($"[{DateTime.Now:HH:mm:ss}] Found {agents.Count} agent(s)");
                foreach (var a in agents)
                    LogMuted($"  {a.DisplayName} — {string.Join(", ", a.Capabilities)}");
            }
            else
            {
                LogMuted($"[{DateTime.Now:HH:mm:ss}] No agents found on ports 9000-9009");
                StatusMessage = "Scan complete — no agents found.";
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Inspector scan failed");
            StatusMessage = $"Scan failed: {ex.Message}";
            LogError($"[{DateTime.Now:HH:mm:ss}] Scan error: {ex.Message}");
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
    private async Task ClearConsoleAsync()
    {
        if (ShowConfirmAsync is not null)
        {
            var ok = await ShowConfirmAsync(
                "Clear Console",
                "Clear all console output? Log entries cannot be restored.",
                "Clear", "Cancel",
                "avares://XBVault/Assets/Views/ErrorDialog/errordialog-clear-20.png",
                "avares://XBVault/Assets/Views/ErrorDialog/errordialog-clear-48.png");
            if (!ok) return;
        }
        ConsoleEntries.Clear();
    }

    [RelayCommand]
    private async Task SaveConsoleAsync()
    {
        var suggestedName = $"inspector-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        var path = ShowSaveFileDialogAsync is not null
            ? await ShowSaveFileDialogAsync(suggestedName)
            : null;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            await File.WriteAllLinesAsync(path, ConsoleEntries.Select(e => e.Text));
            LogMuted($"[{DateTime.Now:HH:mm:ss}] Console saved to {path}");
        }
        catch (Exception ex)
        {
            LogError($"[{DateTime.Now:HH:mm:ss}] Save failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var input = ReplInput?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        ReplInput = "";
        LogCmd($"[{DateTime.Now:HH:mm:ss}] > {input}");

        if (input.StartsWith(':'))
        {
            var cmd = input[1..].ToLowerInvariant();
            switch (cmd)
            {
                case "clear":
                    ConsoleEntries.Clear();
                    return;
                case "scan":
                    await ScanAsync();
                    return;
                case "help":
                    ShowHelp();
                    return;
                default:
                    LogWarn($"[{DateTime.Now:HH:mm:ss}] Unknown local command: {input}");
                    return;
            }
        }

        if (!IsAgentConnected || SelectedSession is null)
        {
            LogWarn($"[{DateTime.Now:HH:mm:ss}] No connected agent — select one from the list.");
            return;
        }

        try
        {
            var id = Guid.NewGuid().ToString("N");
            LogMuted($"[{DateTime.Now:HH:mm:ss}] >>> sending to agent (id={id[..8]}...)");
            await _agentService.SendReplEvalAsync(input, id);
        }
        catch (Exception ex)
        {
            LogError($"[{DateTime.Now:HH:mm:ss}] Send failed: {ex.Message}");
        }
    }

    private void DisconnectAgent()
    {
        _agentService.Disconnect();
        if (SelectedSession is not null)
            SelectedSession.IsConnected = false;
        IsAgentConnected = false;
        LogMuted($"[{DateTime.Now:HH:mm:ss}] Disconnected from agent");
    }

    partial void OnSelectedSessionChanged(XrayAgentInfo? value)
    {
        if (IsAgentConnected)
            DisconnectAgent();

        if (value is null) return;

        var host = ScannedHost;
        if (string.IsNullOrEmpty(host)) return;

        _ = ConnectToAgentAsync(host, value);
    }

    private async Task ConnectToAgentAsync(string host, XrayAgentInfo agent)
    {
        LogMuted($"[{DateTime.Now:HH:mm:ss}] Connecting to {agent.DisplayName}...");
        var ok = await _agentService.ConnectAsync(host, agent.Port);
        if (ok)
        {
            agent.IsConnected = true;
            IsAgentConnected = true;
            LogSuccess($"[{DateTime.Now:HH:mm:ss}] Connected to {agent.DisplayName}");
        }
        else
        {
            LogError($"[{DateTime.Now:HH:mm:ss}] Failed to connect to {agent.DisplayName}");
            SelectedSession = null;
        }
    }

    [RelayCommand]
    private void ShowHelp()
    {
        Log("");
        LogCmd("=== INSPECTOR REPL HELP ===");
        Log("  :help             Show this help");
        Log("  :clear            Clear console");
        Log("  :scan             Run agent discovery scan");
        Log("");
        Log("Commands without ':' prefix are sent to the");
        Log("selected agent for remote execution.");
        Log("");
        Log("Select a discovered agent from the agent list to");
        Log("send commands to that agent.");
        Log("");
        Log("  guide             Open Inspector documentation page");
        Log("");
        Log("==========================");
        Log("");
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
