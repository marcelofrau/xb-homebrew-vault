using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public SettingsViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        LoadSettings();
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

    [RelayCommand]
    private void SaveSettings()
    {
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

        ConnectionStatus = "Saved";
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTestingConnection = true;
        ConnectionStatus = "Testing...";

        _xboxService.Configure(
            $"{(UseHttps ? "https" : "http")}://{Address}:{Port}",
            Username, Password);

        var result = await _xboxService.TestConnectionAsync();

        IsConnected = result;
        ConnectionStatus = result ? "Connected" : "Failed";
        IsTestingConnection = false;
    }
}
