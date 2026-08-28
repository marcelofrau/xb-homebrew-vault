#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;
using Avalonia.Input;

namespace XBVault.ViewModels;

public partial class SystemInfoViewModel : ObservableObject
{
    private const string IconBase = "avares://XBVault/Assets/Views/SystemInfoWindow/";

    private readonly IXboxAuthService _authService;
    private readonly IXboxSystemService _systemService;

    public SystemInfoViewModel(IXboxAuthService authService, IXboxSystemService systemService)
    {
        _authService = authService;
        _systemService = systemService;
    }

    public void Initialize()
    {
        if (!_authService.IsConnected) return;
        _ = RefreshCommand.ExecuteAsync(null);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isLoading;

    public Cursor? Cursor => IsLoading ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _machineName;

    [ObservableProperty]
    private string? _headerSubtitle;

    [ObservableProperty]
    private string? _osVersion;

    [ObservableProperty]
    private string? _devkitCertText;

    [ObservableProperty]
    private string? _lastUpdated;

    [ObservableProperty]
    private string? _consoleType;

    [ObservableProperty]
    private string? _osEdition;

    [ObservableProperty]
    private string? _devMode;

    [ObservableProperty]
    private string? _consoleId;

    [ObservableProperty]
    private string? _serialNumber;

    [ObservableProperty]
    private string? _deviceId;

    [ObservableProperty]
    private IReadOnlyList<SystemInfoCard> _cards = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = null;

        try
        {
            var consoleTask = _systemService.GetConsoleInfoAsync();
            var machineTask = _systemService.GetMachineNameAsync();
            var settingsTask = _systemService.GetXboxSettingsAsync();
            await Task.WhenAll(consoleTask, machineTask, settingsTask);
            var console = await consoleTask;
            var machine = await machineTask;
            var settings = await settingsTask;

            if (console is null && machine is null && settings.Count == 0)
            {
                StatusMessage = "Failed to get system info from the console.";
                HasError = true;
                return;
            }

            Apply(console, machine, settings);
            LastUpdated = $"Updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = "System info failed";
            HasError = true;
            Logger.Error(ex, "RefreshSystemInfo failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(ConsoleInfo? console, string? machine, IReadOnlyList<XboxSetting> settings)
    {
        string Fallback(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v;

        MachineName = Fallback(machine ?? settings.FirstOrDefault(s => s.Name == "Hostname")?.Value);
        HeaderSubtitle = string.Join("  ·  ", new[]
        {
            console?.OsEdition,
            console?.ConsoleType,
            console?.DevMode
        }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (string.IsNullOrWhiteSpace(HeaderSubtitle))
            HeaderSubtitle = null;

        OsVersion = Fallback(console?.OsVersion);

        ConsoleType = Fallback(console?.ConsoleType);
        OsEdition = Fallback(console?.OsEdition);
        DevMode = Fallback(console?.DevMode);
        ConsoleId = Fallback(console?.ConsoleId);
        SerialNumber = Fallback(console?.SerialNumber);
        DeviceId = Fallback(console?.DeviceId);

        if (console?.DevkitCertExpiration is { } exp)
        {
            var days = (exp - DateTimeOffset.UtcNow).Days;
            DevkitCertText = days >= 0
                ? $"Dev Mode until {exp:yyyy-MM-dd} ({days} days left)"
                : $"Dev Mode expired {exp:yyyy-MM-dd}";
        }
        else
        {
            DevkitCertText = "—";
        }

        var byName = settings.ToDictionary(s => s.Name ?? "", StringComparer.OrdinalIgnoreCase);
        string? GetVal(string name) =>
            byName.TryGetValue(name, out var s) && !string.IsNullOrWhiteSpace(s?.Value) ? s!.Value : null;
        bool? GetBool(string name) => bool.TryParse(GetVal(name), out var b) ? b : null;

        SystemInfoRow Row(string label, string? value) =>
            new(label, string.IsNullOrWhiteSpace(value) ? "—" : value, SystemInfoRowBadge.None);
        SystemInfoRow Toggle(string label, bool? on) =>
            on is bool b
                ? new SystemInfoRow(label, b ? "On" : "Off", b ? SystemInfoRowBadge.Positive : SystemInfoRowBadge.Negative)
                : new SystemInfoRow(label, "—", SystemInfoRowBadge.None);
        SystemInfoRow Accent(string label, string value) =>
            new(label, string.IsNullOrWhiteSpace(value) ? "—" : value, SystemInfoRowBadge.Highlight);

        Cards =
        [
            new SystemInfoCard("Display", IconBase + "systeminfo-display-20.png",
            [
                Accent("TV Resolution", GetVal("TvResolution") ?? "—"),
                Row("Color Depth", GetVal("ColorDepth")),
                Row("Color Space", GetVal("ColorSpace")),
                Row("Connection", GetVal("DisplayConnection")),
                Toggle("4K", GetBool("Allow4K")),
                Toggle("HDR", GetBool("AllowHDR")),
                Toggle("VRR", GetBool("AllowVRR")),
                Toggle("Dolby Vision", GetBool("AllowDolbyVision")),
                Toggle("YCC 4:2:2", GetBool("AllowYCC")),
                Toggle("Auto Low Latency", GetBool("AllowAutoLowLatency"))
            ]),

            new SystemInfoCard("Audio", IconBase + "systeminfo-audio-20.png",
            [
                Row("HDMI Audio", GetVal("HDMIAudio")),
                Row("Headset Format", GetVal("HeadsetFormat")),
                Toggle("Audio Passthrough", GetBool("AllowPassthrough")),
                Toggle("Mute Media Audio", GetBool("MuteNotifyForMedia")),
                Toggle("Mute Notifications", GetBool("MuteNotifyForGeneral")),
                Toggle("Muted with Headset", GetBool("IsHdaudioMutedWithHeadset")),
                Toggle("Mono Output", GetBool("MonoOutput"))
            ]),

            new SystemInfoCard("Power", IconBase + "systeminfo-power-20.png",
            [
                Row("Power Mode", GetVal("PowerMode")),
                Toggle("Auto Boot", GetBool("AutoBoot")),
                Toggle("Always On", GetBool("AlwaysOn")),
                Row("Shutdown Timeout", GetVal("ShutdownTimeout")),
                Row("Dim Timeout", GetVal("DimTimeout"))
            ]),

            new SystemInfoCard("Network", IconBase + "systeminfo-network-20.png",
            [
                Row("Hostname", GetVal("Hostname")),
                Row("Wireless Radio", GetVal("WirelessRadioSettings")),
                Row("Default Port", GetVal("DesiredPreferredLocalUDPMultiplayerPort")),
                Row("Current Port", GetVal("CurrentPreferredLocalUDPMultiplayerPort"))
            ]),

            new SystemInfoCard("System & User", IconBase + "systeminfo-devkit-20.png",
            [
                Row("Sandbox ID", GetVal("SandboxId")),
                Row("Region", GetVal("GeographicRegion")),
                Row("Language", GetVal("PreferredLanguages")),
                Row("Time Zone", GetVal("TimeZone")),
                Row("Default Behavior", GetVal("DefaultBehavior")),
                Row("Auto Sign-In User", GetVal("AutoSignInUser"))
            ]),

            new SystemInfoCard("Dev Kit", IconBase + "systeminfo-devkit-20.png",
            [
                Row("Dev Mode", console?.DevMode),
                Accent("Dev Host Expiration", DevkitCertText)
            ])
        ];
    }
}
