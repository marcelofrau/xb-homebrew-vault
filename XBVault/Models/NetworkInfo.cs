using CommunityToolkit.Mvvm.ComponentModel;

namespace XBVault.Models;

public partial class NetworkAdapter : ObservableObject
{
    [ObservableProperty]
    private string _description = "-";

    [ObservableProperty]
    private string _hardwareAddress = "-";

    [ObservableProperty]
    private string _typeName = "-";

    [ObservableProperty]
    private string? _linkSpeed;

    [ObservableProperty]
    private string? _ipAddressesDisplay;

    [ObservableProperty]
    private string? _gatewaysDisplay;

    [ObservableProperty]
    private string? _dnsDisplay;

    [ObservableProperty]
    private string? _dhcpDisplay;

    public bool IsWifi => TypeName?.Contains("Wireless", StringComparison.OrdinalIgnoreCase) == true;

    public bool HasIp => !string.IsNullOrEmpty(IpAddressesDisplay);
}

public partial class WifiNetwork : ObservableObject
{
    [ObservableProperty]
    private string _ssid = "-";

    [ObservableProperty]
    private int _signalQuality;

    [ObservableProperty]
    private string _authentication = "-";

    [ObservableProperty]
    private bool _isConnected;

    public string SignalBars
    {
        get
        {
            if (SignalQuality >= 80) return "■■■■";
            if (SignalQuality >= 60) return "■■■ ";
            if (SignalQuality >= 30) return "■■  ";
            if (SignalQuality >= 10) return "■   ";
            return "    ";
        }
    }

    public bool IsSecured => Authentication is not null
        && !Authentication.Contains("Open", StringComparison.OrdinalIgnoreCase)
        && Authentication != "-";

    public string SignalColor
    {
        get
        {
            if (SignalQuality >= 70) return "#4CAF50";
            if (SignalQuality >= 40) return "#FF9800";
            return "#F44336";
        }
    }
}
