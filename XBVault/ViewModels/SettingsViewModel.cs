#nullable enable
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

/// <summary>
/// Owns editable application settings, dirty-state tracking, cache actions, and settings-related commands.
/// </summary>
/// <remarks>
/// This ViewModel should remain platform-neutral. UI actions such as confirmation dialogs or navigation to logs
/// are injected as delegates by the active frontend.
/// </remarks>
public partial class SettingsViewModel : ObservableObject
{
    private const int AutoHideNotificationDelayMs = 3000;

    private readonly IXboxAuthService _authService;
    private readonly CacheService _cacheService;
    private readonly string _appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XBVault");

    // Called to show the full ConnectionWindow dialog for testing
    public Func<Task<bool>>? ShowConnectDialogAsync { get; set; }

    public Func<string, string, string, string, string?, string?, Task<bool>>? ShowConfirmAsync { get; set; }

    public SettingsViewModel(IXboxAuthService authService, CacheService cacheService)
    {
        _authService = authService;
        _cacheService = cacheService;
        LoadSettings();
        CaptureSnapshot();
        UpdateCacheInfo();
        Logger.Debug("SettingsViewModel initialized");
    }

    // Snapshot of the last-persisted form state, used for dirty tracking
    private string _savedAddress = string.Empty;
    private string _savedPort = "11443";
    private string _savedUsername = string.Empty;
    private string _savedPassword = string.Empty;
    private bool _savedUseHttps = true;
    private string _savedLogLevel = "Info";
    private int _savedUiScalePercent = 100;
    private bool _savedAutoConnect;
    private int _savedUpdateCheckIntervalMinutes = 30;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    private void CaptureSnapshot()
    {
        _savedAddress = Address;
        _savedPort = Port;
        _savedUsername = Username;
        _savedPassword = Password;
        _savedUseHttps = UseHttps;
        _savedLogLevel = SelectedLogLevel;
        _savedUiScalePercent = UiScalePercent;
        _savedAutoConnect = AutoConnect;
        _savedUpdateCheckIntervalMinutes = UpdateCheckIntervalMinutes;
        HasUnsavedChanges = false;
        Logger.Debug("Dirty snapshot captured");
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Address) or nameof(Port) or nameof(Username)
            or nameof(Password) or nameof(UseHttps) or nameof(SelectedLogLevel)
            or nameof(UiScalePercent) or nameof(AutoConnect) or nameof(UpdateCheckIntervalMinutes))
        {
            RefreshDirtyState();
        }
    }

    private void RefreshDirtyState()
    {
        var dirty = Address != _savedAddress
            || Port != _savedPort
            || Username != _savedUsername
            || Password != _savedPassword
            || UseHttps != _savedUseHttps
            || SelectedLogLevel != _savedLogLevel
            || UiScalePercent != _savedUiScalePercent
            || AutoConnect != _savedAutoConnect
            || UpdateCheckIntervalMinutes != _savedUpdateCheckIntervalMinutes;
        if (dirty != HasUnsavedChanges)
            HasUnsavedChanges = dirty;
    }

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _port = "11443";

    partial void OnPortChanged(string value)
    {
        ValidatePort();
    }

    [ObservableProperty]
    private string _portError = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _useHttps = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isTestingConnection;

    [ObservableProperty]
    private string _connectionStatus = "Not configured";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _showSavedNotification;

    [ObservableProperty]
    private string _savedNotificationText = string.Empty;

    partial void OnShowSavedNotificationChanged(bool value)
    {
        if (value)
            _ = AutoHideSavedNotification();
    }

    private async Task AutoHideSavedNotification()
    {
        await Task.Delay(AutoHideNotificationDelayMs);
        ShowSavedNotification = false;
    }

    [ObservableProperty]
    private long _cacheSizeBytes;

    [ObservableProperty]
    private string _cacheSizeText = "0 B";

    [ObservableProperty]
    private string _selectedLogLevel = "Info";

    [ObservableProperty]
    private int _uiScalePercent = 100;

    [ObservableProperty]
    private bool _autoConnect;

    partial void OnAutoConnectChanged(bool value)
    {
        SettingsService.Current.AutoConnect = value;
        Logger.Info($"Auto-connect set to {value}");
    }

    [ObservableProperty]
    private int _updateCheckIntervalMinutes = 30;

    public List<int> UpdateCheckIntervals { get; } = [15, 30, 60, 120, 240];

    partial void OnUpdateCheckIntervalMinutesChanged(int value)
    {
        SettingsService.Current.UpdateCheckIntervalMinutes = value;
        Logger.Info($"Update check interval set to {value} minutes");
    }

    // Invoked whenever the UI scale changes so live windows can re-apply it
    public Action? UiScaleChanged { get; set; }

    public List<int> UiScaleOptions { get; } = [80, 90, 100, 110, 120];

    partial void OnUiScalePercentChanged(int value)
    {
        SettingsService.Current.UiScale = value / 100.0;
        UiScaleChanged?.Invoke();
        Logger.Info($"UI scale set to {value}%");
    }

    public Cursor? Cursor => IsTestingConnection ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    public List<string> LogLevels { get; } = ["Trace", "Debug", "Info", "Warn", "Error", "Fatal"];

    partial void OnSelectedLogLevelChanged(string value)
    {
        Logger.Debug($"Log level changed to {value}");
        Logger.MinLevel = value switch
        {
            "Trace" => LogLevel.Trace,
            "Debug" => LogLevel.Debug,
            "Info"  => LogLevel.Info,
            "Warn"  => LogLevel.Warn,
            "Error" => LogLevel.Error,
            "Fatal" => LogLevel.Fatal,
            _       => LogLevel.Info
        };
        SettingsService.Current.MinLogLevel = value;
        Logger.Info($"Log level set to {value}");
    }

    private void LoadSettings()
    {
        LoadFrom(SettingsService.Current);
    }

    private void LoadFrom(AppSettings settings)
    {
        Logger.Debug("Loading settings");
        var conn = settings.XboxConnection;

        Address = conn.Address;
        Port = conn.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Username = conn.Username;
        UseHttps = conn.UseHttps;
        SelectedLogLevel = settings.MinLogLevel;
        AutoConnect = settings.AutoConnect;
        UpdateCheckIntervalMinutes = settings.UpdateCheckIntervalMinutes;
        var savedScale = (int)Math.Round(settings.UiScale * 100);
#pragma warning disable MVVMTK0034
        _uiScalePercent = UiScaleOptions.OrderBy(o => Math.Abs(o - savedScale)).First();
#pragma warning restore MVVMTK0034

        Password = string.IsNullOrEmpty(conn.EncryptedPassword)
            ? string.Empty
            : CryptoService.Deobfuscate(conn.EncryptedPassword);

        if (conn.IsConfigured)
        {
            Logger.Debug("Connection already configured, applying");
            _authService.Configure(conn.BaseUrl, conn.Username,
                CryptoService.Deobfuscate(conn.EncryptedPassword));
            ConnectionStatus = "Configured";
        }
        else
        {
            ConnectionStatus = "Not configured";
        }
    }

    private void ValidatePort()
    {
        if (string.IsNullOrWhiteSpace(Port))
        {
            PortError = "Port is required";
            return;
        }

        if (!int.TryParse(Port, out var portVal))
        {
            PortError = "A number is expected";
            return;
        }

        if (portVal < 1 || portVal > 65535)
        {
            PortError = "Port must be 1-65535";
            return;
        }

        PortError = string.Empty;
    }

    private bool TryGetPort(out int portVal)
    {
        portVal = 0;
        return !string.IsNullOrWhiteSpace(Port)
            && int.TryParse(Port, out portVal)
            && portVal >= 1 && portVal <= 65535;
    }

    private void UpdateCacheInfo()
    {
        CacheSizeBytes = _cacheService.GetCacheSizeBytes();
        CacheSizeText = FormatBytes(CacheSizeBytes);
        Logger.Debug($"Cache size: {CacheSizeText}");
    }

    private static string FormatBytes(long bytes)
    {
        // InvariantCulture: "1.5 GB" regardless of pt-BR comma vs en-US dot
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{(bytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture)} KB",
            < 1024 * 1024 * 1024 => $"{(bytes / (1024.0 * 1024)).ToString("F1", CultureInfo.InvariantCulture)} MB",
            _ => $"{(bytes / (1024.0 * 1024 * 1024)).ToString("F2", CultureInfo.InvariantCulture)} GB"
        };
    }

    [RelayCommand]
    private void DismissSavedNotification()
    {
        ShowSavedNotification = false;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        Logger.Debug("SaveSettings called");

        var settings = SettingsService.Current;
        var wantsConnection = !string.IsNullOrWhiteSpace(Address)
            || !string.IsNullOrWhiteSpace(Username)
            || !string.IsNullOrWhiteSpace(Password);

        if (wantsConnection)
        {
            if (string.IsNullOrWhiteSpace(Address))
            {
                ConnectionStatus = "Address is required";
                Logger.Warn("Save aborted: address empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                ConnectionStatus = "Username is required";
                Logger.Warn("Save aborted: username empty");
                return;
            }

            if (!TryGetPort(out var portVal))
            {
                if (string.IsNullOrWhiteSpace(PortError))
                    ConnectionStatus = "Port must be 1-65535";
                Logger.Warn("Save aborted: invalid port");
                return;
            }

            var obfuscated = CryptoService.Obfuscate(Password);
            settings.XboxConnection.Address = Address;
            settings.XboxConnection.Port = portVal;
            settings.XboxConnection.Username = Username;
            settings.XboxConnection.EncryptedPassword = obfuscated;
            settings.XboxConnection.UseHttps = UseHttps;

            var baseUrl = $"{(UseHttps ? "https" : "http")}://{Address}:{Port}";
            _authService.Configure(baseUrl, Username, Password);
            Logger.Info($"Connection settings saved: {Address}:{Port} (HTTPS={UseHttps})");
        }

        settings.MinLogLevel = SelectedLogLevel;
        settings.UiScale = UiScalePercent / 100.0;
        settings.AutoConnect = AutoConnect;
        settings.UpdateCheckIntervalMinutes = UpdateCheckIntervalMinutes;

        SettingsService.Save();
        Logger.Info("Settings saved");
        CaptureSnapshot();

        SavedNotificationText = "Settings saved successfully!";
        ShowSavedNotification = true;
        ConnectionStatus = string.Empty;
    }

    [RelayCommand]
    private void DiscardChanges()
    {
        Logger.Debug("DiscardChanges called");
        SettingsService.Load();
        LoadSettings();
        CaptureSnapshot();
        UiScaleChanged?.Invoke();
        SavedNotificationText = "Changes discarded";
        ShowSavedNotification = true;
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        Logger.Debug("ResetToDefaults called");
        LoadFrom(new AppSettings());
        SettingsService.Current.UiScale = UiScalePercent / 100.0;
        UiScaleChanged?.Invoke();
        RefreshDirtyState();
        SavedNotificationText = "Form reset to defaults — press Save to apply";
        ShowSavedNotification = true;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        Logger.Debug("TestConnectionAsync started");

        if (string.IsNullOrWhiteSpace(Address))
        {
            ConnectionStatus = "Enter an address first";
            Logger.Warn("Test aborted: no address");
            return;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            ConnectionStatus = "Enter a username first";
            Logger.Warn("Test aborted: no username");
            return;
        }

        if (!TryGetPort(out var portVal))
        {
            ConnectionStatus = "Enter a valid port";
            Logger.Warn("Test aborted: invalid port");
            return;
        }

        if (ShowConnectDialogAsync is null)
        {
            Logger.Warn("ShowConnectDialogAsync not set — falling back to simple test");
            IsTestingConnection = true;
            ConnectionStatus = "Testing...";
            var baseUrl = $"{(UseHttps ? "https" : "http")}://{Address}:{Port}";
            _authService.Configure(baseUrl, Username, Password);
            var result = await _authService.TestConnectionAsync();
            IsConnected = result.Success;
            ConnectionStatus = result.Success ? "Connected" : "Connection failed";
            IsTestingConnection = false;
            return;
        }

        // Save current form values to settings in-memory, then open full connect dialog
        Logger.Info($"Opening connect dialog for {Address}:{Port}");
        var obfuscated = CryptoService.Obfuscate(Password);
        var settings = SettingsService.Current;
        settings.XboxConnection.Address = Address;
        settings.XboxConnection.Port = portVal;
        settings.XboxConnection.Username = Username;
        settings.XboxConnection.EncryptedPassword = obfuscated;
        settings.XboxConnection.UseHttps = UseHttps;

        var result2 = await ShowConnectDialogAsync();

        IsConnected = result2;
        IsTestingConnection = false;

        if (result2)
        {
            ConnectionStatus = "Connected";
            Logger.Info("Connection via dialog succeeded");
        }
        else
        {
            ConnectionStatus = "Connection failed — check address and credentials";
            Logger.Warn("Connection via dialog failed");
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        if (ShowConfirmAsync is not null)
        {
            var ok = await ShowConfirmAsync("Clear Cache", "Clear the local package cache? Cached files will be deleted and re-downloaded as needed.", "Clear", "Cancel", null, "avares://XBVault/Assets/Views/ErrorDialog/errordialog-clear-48.png");
            if (!ok) return;
        }
        Logger.Debug("ClearCache called");
        var oldSize = CacheSizeText;
        _cacheService.ClearCache();
        UpdateCacheInfo();
        Logger.Info($"Cache cleared (was {oldSize})");
        ConnectionStatus = "Cache cleared";
    }

    [RelayCommand]
    private async Task RestartAppAsync()
    {
        if (ShowConfirmAsync is not null)
        {
            var ok = await ShowConfirmAsync("Restart Application", "Are you sure you want to restart the app? All unsaved changes will be lost.", "Restart", "Cancel", null, "avares://XBVault/Assets/Views/ErrorDialog/errordialog-restart-app-48.png");
            if (!ok) return;
        }
        Logger.Info("RestartApp called — launching new process");
        var exe = Environment.ProcessPath;
        if (exe is not null)
            Process.Start(exe);
        Environment.Exit(0);
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        if (ShowConfirmAsync is not null)
        {
            var ok = await ShowConfirmAsync("Reset Settings", "Reset all settings to defaults? Saved connection, preferences, and log level will be cleared.", "Reset", "Cancel", null, "avares://XBVault/Assets/Views/ErrorDialog/errordialog-settings-48.png");
            if (!ok) return;
        }
        Logger.Info("ResetSettings called");
        SettingsService.Reset();
        LoadSettings();
        CaptureSnapshot();
        SavedNotificationText = "Settings reset to defaults";
        ShowSavedNotification = true;
    }

    [RelayCommand]
    private async Task ResetWindowSizeAsync()
    {
        if (ShowConfirmAsync is not null)
        {
            var ok = await ShowConfirmAsync("Reset Window Size", "Reset the saved main window size? The default size will be used next time the app starts.", "Reset", "Cancel", null, "avares://XBVault/Assets/Views/ErrorDialog/errordialog-restart-app-48.png");
            if (!ok) return;
        }

        WindowSettingsService.ResetMainWindowSize();
        SavedNotificationText = "Window size reset. Restart the app to apply default size.";
        ShowSavedNotification = true;
        Logger.Info("Main window size reset to defaults");
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        Logger.Debug("OpenSettingsFolder called");
        var dir = _appDataDir;
        if (Directory.Exists(dir))
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    // Invoked by the GoToLogs command to switch to the Logs screen
    public Action? ShowLogsAction { get; set; }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Logger.Debug("OpenLogsFolder called");
        var dir = Path.Combine(_appDataDir, "logs");
        if (Directory.Exists(dir))
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        else
            Logger.Warn("OpenLogsFolder: log directory not found");
    }

    [RelayCommand]
    private void GoToLogs()
    {
        Logger.Info("GoToLogs called");
        ShowLogsAction?.Invoke();
    }
}
