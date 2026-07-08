using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class InspectorViewModel : ObservableObject
{
    private const double DefaultConsoleFontSize = 13;
    private const double MinConsoleFontSize = 10;
    private const double MaxConsoleFontSize = 24;

    private static readonly IBrush _defaultBrush = new SolidColorBrush(0xFFF0F0F0);
    private static readonly IBrush _mutedBrush = new SolidColorBrush(0xFF8B8D91);
    private static readonly IBrush _errorBrush = new SolidColorBrush(0xFFE74C3C);
    private static readonly IBrush _successBrush = new SolidColorBrush(0xFF2ECC71);
    private static readonly IBrush _warnBrush = new SolidColorBrush(0xFFF39C12);
    private static readonly IBrush _accentBrush = new SolidColorBrush(0xFF9ACA3C);

    private readonly XboxDeviceService _xboxService;

    public InspectorViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        _xboxService.ConnectionChanged += OnConnectionChanged;
        ConsoleFontSize = ClampConsoleFontSize(SettingsService.Current.ConsoleFontSize);
        IsConnected = _xboxService.IsConnected;
        ConsoleEntries.CollectionChanged += (_, _) => RebuildFilteredEntries();
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
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
    private string? _selectedSession;

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
    private int _filterLinesAbove = 3;

    [ObservableProperty]
    private int _filterLinesBelow = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterMatchText))]
    private int _filterMatchCount;

    public string FilterMatchText =>
        string.IsNullOrEmpty(FilterText) ? "" : $"{FilterMatchCount} matches";

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

    public ObservableCollection<InspectorConsoleEntry> ConsoleEntries { get; } = new();
    public ObservableCollection<InspectorConsoleEntry> FilteredConsoleEntries { get; } = new();
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
    public Func<string, string, string, string, string?, string?, Task<bool>>? ShowConfirmAsync { get; set; }
    public Func<string, Task<string?>>? ShowSaveFileDialogAsync { get; set; }

    partial void OnFilterTextChanged(string value) => RebuildFilteredEntries();
    partial void OnFilterLinesAboveChanged(int value) => RebuildFilteredEntries();
    partial void OnFilterLinesBelowChanged(int value) => RebuildFilteredEntries();

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

    [RelayCommand]
    private void IncrementFilterLinesAbove()
    {
        if (FilterLinesAbove < 10)
            FilterLinesAbove++;
    }

    [RelayCommand]
    private void DecrementFilterLinesAbove()
    {
        if (FilterLinesAbove > 0)
            FilterLinesAbove--;
    }

    [RelayCommand]
    private void IncrementFilterLinesBelow()
    {
        if (FilterLinesBelow < 10)
            FilterLinesBelow++;
    }

    [RelayCommand]
    private void DecrementFilterLinesBelow()
    {
        if (FilterLinesBelow > 0)
            FilterLinesBelow--;
    }

    private void RebuildFilteredEntries()
    {
        FilteredConsoleEntries.Clear();
        FilterMatchCount = 0;

        var filter = FilterText?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            foreach (var e in ConsoleEntries)
            {
                e.IsMatch = false;
                FilteredConsoleEntries.Add(e);
            }
            return;
        }

        var above = FilterLinesAbove;
        var below = FilterLinesBelow;
        var matchRanges = new List<(int start, int end)>();

        for (int i = 0; i < ConsoleEntries.Count; i++)
        {
            if (ConsoleEntries[i].Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                var start = Math.Max(0, i - above);
                var end = Math.Min(ConsoleEntries.Count - 1, i + below);
                matchRanges.Add((start, end));
            }
        }

        if (matchRanges.Count == 0) return;

        var merged = new List<(int start, int end)> { matchRanges[0] };
        for (int i = 1; i < matchRanges.Count; i++)
        {
            if (matchRanges[i].start <= merged[^1].end + 1)
                merged[^1] = (merged[^1].start, Math.Max(merged[^1].end, matchRanges[i].end));
            else
                merged.Add(matchRanges[i]);
        }

        var directMatch = new HashSet<int>();
        for (int i = 0; i < ConsoleEntries.Count; i++)
        {
            if (ConsoleEntries[i].Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
                directMatch.Add(i);
        }

        foreach (var (start, end) in merged)
        {
            for (int i = start; i <= end; i++)
            {
                var entry = ConsoleEntries[i];
                entry.IsMatch = directMatch.Contains(i);
                FilteredConsoleEntries.Add(entry);
            }
        }

        FilterMatchCount = directMatch.Count;
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
            LogMuted($"[{DateTime.Now:HH:mm:ss}] Scanning ports 9000-9010 on {ScannedHost}...");
            await Task.Delay(500);
            LogMuted($"[{DateTime.Now:HH:mm:ss}] Scan complete — no agents found.");
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

        if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleEntries.Clear();
            return;
        }

        // TODO: forward command to selected agent session
        LogWarn($"[{DateTime.Now:HH:mm:ss}] No active session — command not sent.");
    }

    [RelayCommand]
    private void ShowHelp()
    {
        Log("");
        LogCmd("=== INSPECTOR REPL HELP ===");
        Log("  help              Show this help");
        Log("  clear             Clear console");
        Log("  scan              Run agent discovery scan");
        Log("  connect           Open connection dialog");
        Log("  status            Show connection info");
        Log("  <command>         Send raw command to selected agent");
        Log("");
        Log("Select a discovered agent from the channel list to");
        Log("target commands to that agent.");
        Log("");
        Log("  guide             Open Inspector documentation page");
        Log("");
        LogWarn("⚠ Under development & testing — scan and REPL are stubs.");
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
