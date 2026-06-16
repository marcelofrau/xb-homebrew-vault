using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private readonly CacheService _cacheService;

    public SettingsViewModel(XboxDeviceService xboxService, CacheService cacheService)
    {
        _xboxService = xboxService;
        _cacheService = cacheService;
        LoadSettings();
        UpdateCacheInfo();
    }

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private int _port = 11443;

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
    private long _cacheSizeBytes;

    [ObservableProperty]
    private string _cacheSizeText = "0 B";

    private void LoadSettings()
    {
        var settings = SettingsService.Current;
        var conn = settings.XboxConnection;

        Address = conn.Address;
        Port = conn.Port;
        Username = conn.Username;
        UseHttps = conn.UseHttps;

        if (!string.IsNullOrEmpty(conn.EncryptedPassword))
            Password = CryptoService.Deobfuscate(conn.EncryptedPassword);

        if (conn.IsConfigured)
        {
            _xboxService.Configure(conn.BaseUrl, conn.Username,
                CryptoService.Deobfuscate(conn.EncryptedPassword));
            ConnectionStatus = "Configured";
        }
    }

    private void UpdateCacheInfo()
    {
        CacheSizeBytes = _cacheService.GetCacheSizeBytes();
        CacheSizeText = FormatBytes(CacheSizeBytes);
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
    private void SaveSettings()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            ConnectionStatus = "Address is required";
            return;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            ConnectionStatus = "Username is required";
            return;
        }

        if (Port < 1 || Port > 65535)
        {
            ConnectionStatus = "Port must be 1-65535";
            return;
        }

        var obfuscated = CryptoService.Obfuscate(Password);

        var settings = SettingsService.Current;
        settings.XboxConnection.Address = Address;
        settings.XboxConnection.Port = Port;
        settings.XboxConnection.Username = Username;
        settings.XboxConnection.EncryptedPassword = obfuscated;
        settings.XboxConnection.UseHttps = UseHttps;

        SettingsService.Save();

        _xboxService.Configure(
            $"{(UseHttps ? "https" : "http")}://{Address}:{Port}",
            Username, Password);

        ConnectionStatus = "Settings saved";
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            ConnectionStatus = "Enter an address first";
            return;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            ConnectionStatus = "Enter a username first";
            return;
        }

        IsTestingConnection = true;
        ConnectionStatus = "Testing...";

        _xboxService.Configure(
            $"{(UseHttps ? "https" : "http")}://{Address}:{Port}",
            Username, Password);

        var result = await _xboxService.TestConnectionAsync();

        IsConnected = result;
        ConnectionStatus = result ? "Connected" : "Connection failed — check address and credentials";
        IsTestingConnection = false;
    }

    [RelayCommand]
    private void ClearCache()
    {
        _cacheService.ClearCache();
        UpdateCacheInfo();
        ConnectionStatus = "Cache cleared";
    }
}
