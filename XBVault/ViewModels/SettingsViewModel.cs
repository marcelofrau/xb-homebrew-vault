using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const int AutoHideNotificationDelayMs = 3000;

    private readonly XboxDeviceService _xboxService;
    private readonly CacheService _cacheService;

    // Called to show the full ConnectionWindow dialog for testing
    public Func<Task<bool>>? ShowConnectDialogAsync { get; set; }

    public Func<string, string, string, string, string?, string?, Task<bool>>? ShowConfirmAsync { get; set; }

    public SettingsViewModel(XboxDeviceService xboxService, CacheService cacheService)
    {
        _xboxService = xboxService;
        _cacheService = cacheService;
        LoadSettings();
        UpdateCacheInfo();
        Logger.Debug("SettingsViewModel initialized");
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
        SettingsService.Save();
        Logger.Info($"Log level set to {value}");
    }

    private void LoadSettings()
    {
        Logger.Debug("Loading settings from disk");
        var settings = SettingsService.Current;
        var conn = settings.XboxConnection;

        Address = conn.Address;
        Port = conn.Port.ToString();
        Username = conn.Username;
        UseHttps = conn.UseHttps;
        SelectedLogLevel = settings.MinLogLevel;

        if (!string.IsNullOrEmpty(conn.EncryptedPassword))
            Password = CryptoService.Deobfuscate(conn.EncryptedPassword);

        if (conn.IsConfigured)
        {
            Logger.Debug("Connection already configured, applying");
            _xboxService.Configure(conn.BaseUrl, conn.Username,
                CryptoService.Deobfuscate(conn.EncryptedPassword));
            ConnectionStatus = "Configured";
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
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
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

        var settings = SettingsService.Current;
        settings.XboxConnection.Address = Address;
        settings.XboxConnection.Port = portVal;
        settings.XboxConnection.Username = Username;
        settings.XboxConnection.EncryptedPassword = obfuscated;
        settings.XboxConnection.UseHttps = UseHttps;
        settings.MinLogLevel = SelectedLogLevel;

        SettingsService.Save();
        Logger.Info($"Settings saved: {Address}:{Port} (HTTPS={UseHttps})");

        var baseUrl = $"{(UseHttps ? "https" : "http")}://{Address}:{Port}";
        _xboxService.Configure(baseUrl, Username, Password);
        Logger.Debug("XboxDeviceService reconfigured with new settings");

        SavedNotificationText = "Settings saved successfully!";
        ShowSavedNotification = true;
        ConnectionStatus = string.Empty;
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
            _xboxService.Configure(baseUrl, Username, Password);
            var result = await _xboxService.TestConnectionAsync();
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
        SavedNotificationText = "Settings reset to defaults";
        ShowSavedNotification = true;
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        Logger.Debug("OpenSettingsFolder called");
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XBVault");
        if (Directory.Exists(dir))
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }
}
