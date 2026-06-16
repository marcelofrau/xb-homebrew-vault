using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public ConnectionViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        Logger.Debug("ConnectionViewModel initialized");
    }

    public ObservableCollection<string> OutputLines { get; } = new ObservableCollection<string>();

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isFailed;

    public event Action<bool>? Completed;

    private void AddLine(string text)
    {
        OutputLines.Add(text);
        Logger.Info(text);
    }

    private async Task Delay(int ms) => await Task.Delay(ms);

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsRunning) return;

        IsRunning = true;
        IsSuccess = false;
        IsFailed = false;
        Progress = 0;
        OutputLines.Clear();

        var settings = SettingsService.Current.XboxConnection;

        AddLine("ATDT " + settings.Address);
        await Delay(300);

        AddLine("Initializing modem...");
        Progress = 0.1;
        await Delay(500);

        AddLine("Dialing " + settings.BaseUrl + "...");
        await Delay(400);

        AddLine("Waiting for carrier...");
        Progress = 0.25;
        await Delay(600);

        var baseUrl = settings.BaseUrl;
        var pw = string.IsNullOrEmpty(settings.EncryptedPassword)
            ? "" : CryptoService.Deobfuscate(settings.EncryptedPassword);

        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(pw))
        {
            AddLine("");
            AddLine("ERROR: Connection not configured");
            AddLine("Go to Settings and save your Xbox connection first.");
            IsRunning = false;
            IsFailed = true;
            Completed?.Invoke(false);
            return;
        }

        _xboxService.Configure(baseUrl, settings.Username, pw);

        AddLine("CONNECTING...");
        Progress = 0.4;
        await Delay(500);

        AddLine("Negotiating handshake...");
        Progress = 0.55;
        await Delay(400);

        AddLine("Authenticating...");
        Progress = 0.7;
        await Delay(500);

        var result = await _xboxService.TestConnectionAsync();

        if (result)
        {
            AddLine("");
            AddLine("CONNECTED!");
            AddLine("Link established at " + baseUrl);
            Progress = 1.0;
            IsSuccess = true;
        }
        else
        {
            AddLine("");
            AddLine("NO CARRIER");
            AddLine("Connection failed — check address and credentials.");
            IsFailed = true;
        }

        IsRunning = false;
        Completed?.Invoke(result);
    }
}
