using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private CancellationTokenSource? _cts;

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
            await Task.Delay(300, ct);

            AddLine("Initializing modem...");
            Progress = 0.1;
            await Task.Delay(500, ct);

            AddLine("Dialing " + settings.BaseUrl + "...");
            await Task.Delay(400, ct);

            AddLine("Waiting for carrier...");
            Progress = 0.25;
            await Task.Delay(600, ct);

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
                AddLine("");
                AddLine("RING... RING...");
                Progress = 0.3;
                await Task.Delay(300, ct);

                AddLine("CONNECT 33600 bps");
                Progress = 0.35;
                await Task.Delay(250, ct);

                AddLine("Protocol: TCP/IP");
                Progress = 0.4;
                await Task.Delay(250, ct);

                AddLine("Negotiating handshake...");
                Progress = 0.5;
                await Task.Delay(350, ct);

                AddLine("Authenticating...");
                Progress = 0.65;
                await Task.Delay(350, ct);

                AddLine("Obtaining network configuration...");
                Progress = 0.8;
                await Task.Delay(350, ct);

                AddLine("Initializing remote session...");
                Progress = 0.9;
                await Task.Delay(350, ct);

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
