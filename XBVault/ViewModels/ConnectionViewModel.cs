using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private CancellationTokenSource? _cts;
    private static readonly Random _memeRng = new();
    private const int DialToneDelayMs = 300;
    private const int ModemInitDelayMs = 500;
    private const int DialingDelayMs = 400;
    private const int CarrierDelayMs = 600;
    private const int LinkSpeedDelayMs = 300;
    private const int ConnectDisplayDelayMs = 250;
    private const int ProtocolDisplayDelayMs = 250;
    private const int TcpHandshakeDelayMs = 300;
    private const int NegotiateDelayMs = 350;
    private const int AuthDelayMs = 350;
    private const int NetworkConfigDelayMs = 350;
    private const int SessionInitDelayMs = 350;

    public ConnectionViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        Logger.Debug("ConnectionViewModel initialized");
    }

    public ObservableCollection<string> OutputLines { get; } = new ObservableCollection<string>();

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isFailed;

    public bool IsActive => !IsRunning && !IsSuccess;

    public event Action<bool>? Completed;

    private void AddLine(string text)
    {
        OutputLines.Add(text);
        Logger.Info(text);
    }

    private async Task<string?> TryGetLinkSpeedAsync()
    {
        try
        {
            var json = await _xboxService.GetNetworkConfigAsync();
            if (json is null) return null;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Adapters", out var adapters))
                return null;

            foreach (var a in adapters.EnumerateArray())
            {
                if (a.TryGetProperty("LinkSpeed", out var s))
                    return s.GetString();
                if (a.TryGetProperty("Speed", out var s2))
                    return s2.GetString();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"TryGetLinkSpeedAsync: failed to parse: {ex.Message}");
        }
        return null;
    }

    private static readonly string[] CortanaLines =
    {
        "Cortana: Slipspace connection active. Welcome back, Spartan.",
        "Cortana: AI handshake complete. Transmitting coordinates.",
        "Cortana: I've got the maps. Ready when you are.",
        "Cortana: Network secured. Link established.",
        "Cortana: Welcome home, Spartan. Connection stable.",
        "Cortana: AI protocol v1.0 — Handshake successful.",
        "Cortana: UNSC network bridge active.",
        "Cortana: Connection authenticated. Signal secure.",
        "Cortana: Slipspace buffer calibrated. Engaging link.",
        "Cortana: Sending your coordinates. Stay safe out there."
    };

    private async Task MaybeMeme(CancellationToken ct)
    {
        if (_memeRng.NextDouble() >= 0.5) return;
        var lines = MemeLines.All;
        var count = _memeRng.Next(1, 3);
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(_memeRng.Next(150, 400), ct);
            AddLine(lines[_memeRng.Next(lines.Length)]);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsRunning)
        {
            _cts?.Cancel();
        }
        else
        {
            AddLine("CANCELLED — User cancelled connection");
            Completed?.Invoke(false);
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsRunning = true;
        IsSuccess = false;
        IsFailed = false;
        Progress = 0;
        OutputLines.Clear();

        var settings = SettingsService.Current.XboxConnection;

        try
        {
            AddLine("ATDT " + settings.Address);
            await Task.Delay(DialToneDelayMs, ct);
            await MaybeMeme(ct);

            AddLine("Initializing modem...");
            Progress = 0.1;
            await Task.Delay(ModemInitDelayMs, ct);
            await MaybeMeme(ct);

            AddLine("Dialing " + settings.BaseUrl + "...");
            await Task.Delay(DialingDelayMs, ct);

            AddLine("Waiting for carrier...");
            Progress = 0.25;
            await Task.Delay(CarrierDelayMs, ct);
            await MaybeMeme(ct);

            var baseUrl = settings.BaseUrl;
            var pw = string.IsNullOrEmpty(settings.EncryptedPassword)
                ? "" : CryptoService.Deobfuscate(settings.EncryptedPassword);

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(pw))
            {
                AddLine("");
                AddLine("ERROR: Connection not configured");
                AddLine("Go to Settings and save your Xbox connection first.");
                IsFailed = true;
                return;
            }

            _xboxService.Configure(baseUrl, settings.Username, pw);

            var result = await _xboxService.TestConnectionAsync(ct);

            if (result.IsCancelled || ct.IsCancellationRequested)
            {
                AddLine("");
                AddLine("CANCELLED — User cancelled connection");
                IsFailed = true;
            }
            else if (result.Success)
            {
                var linkSpeed = await TryGetLinkSpeedAsync();

                AddLine("");
                AddLine(CortanaLines[_memeRng.Next(CortanaLines.Length)]);
                Progress = 0.3;
                await Task.Delay(LinkSpeedDelayMs, ct);
                await MaybeMeme(ct);

                AddLine("CONNECT " + (linkSpeed ?? "1 Gbps"));
                Progress = 0.35;
                await Task.Delay(ConnectDisplayDelayMs, ct);

                AddLine("Protocol: TCP/IP");
                Progress = 0.4;
                await Task.Delay(ProtocolDisplayDelayMs, ct);

                AddLine("TCP handshake: SYN sent... SYN-ACK received... ACK sent.");
                Progress = 0.45;
                await Task.Delay(TcpHandshakeDelayMs, ct);
                await MaybeMeme(ct);

                AddLine("Negotiating handshake...");
                Progress = 0.5;
                await Task.Delay(NegotiateDelayMs, ct);
                await MaybeMeme(ct);

                AddLine("Authenticating...");
                Progress = 0.65;
                await Task.Delay(AuthDelayMs, ct);

                AddLine("Obtaining network configuration...");
                Progress = 0.8;
                await Task.Delay(NetworkConfigDelayMs, ct);

                AddLine("Initializing remote session...");
                Progress = 0.9;
                await Task.Delay(SessionInitDelayMs, ct);

                AddLine("");
                AddLine("CONNECTED!");
                AddLine("Link established at " + baseUrl);
                Progress = 1.0;
                IsSuccess = true;
            }
            else
            {
                AddLine("");
                var detail = result.ErrorDetail ?? "Unknown error";
                var dialup = detail switch
                {
                    string s when s.Contains("timed out", StringComparison.OrdinalIgnoreCase) => "NO ANSWER",
                    string s when s.Contains("refused", StringComparison.OrdinalIgnoreCase) => "BUSY SIGNAL",
                    string s when s.Contains("unreachable", StringComparison.OrdinalIgnoreCase) => "NUMBER DISCONNECTED",
                    string s when s.Contains("DNS", StringComparison.OrdinalIgnoreCase) || s.Contains("resolve", StringComparison.OrdinalIgnoreCase) => "INVALID NUMBER",
                    string s when s.Contains("401", StringComparison.OrdinalIgnoreCase) => "PASSWORD REJECTED",
                    string s when s.Contains("403", StringComparison.OrdinalIgnoreCase) => "ACCESS DENIED",
                    string s when s.Contains("404", StringComparison.OrdinalIgnoreCase) => "NOT FOUND",
                    _ => "NO CARRIER"
                };
                AddLine(dialup + " — " + detail);
                IsFailed = true;
            }
        }
        catch (OperationCanceledException)
        {
            AddLine("");
            AddLine("CANCELLED — User cancelled connection");
            IsFailed = true;
        }
        finally
        {
            IsRunning = false;
            Completed?.Invoke(IsSuccess);
        }
    }
}
