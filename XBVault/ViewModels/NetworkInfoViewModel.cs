using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class NetworkInfoViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public NetworkInfoViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<NetworkAdapter> Adapters { get; } = [];
    public ObservableCollection<WifiNetwork> WifiNetworks { get; } = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = null;
        Adapters.Clear();
        WifiNetworks.Clear();

        try
        {
            // Network adapters (ipconfig)
            var cfgJson = await _xboxService.GetNetworkConfigAsync();
            if (cfgJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(cfgJson);
                    if (doc.RootElement.TryGetProperty("Adapters", out var adapters))
                        ParseAdapters(adapters);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to parse network config JSON: {ex.Message}");
                }
            }

            // WiFi scan
            var wifiJson = await _xboxService.GetWifiInterfacesAsync();
            Logger.Debug($"WifiInterfaces response: {wifiJson}");
            if (wifiJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(wifiJson);
                    var ifaces = doc.RootElement.TryGetProperty("Interfaces", out var ifProp)
                        ? ifProp
                        : doc.RootElement.TryGetProperty("Result", out var rProp)
                            ? rProp
                            : doc.RootElement;

                    if (ifaces.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var iface in ifaces.EnumerateArray())
                        {
                            var guid = iface.TryGetProperty("GUID", out var g) ? g.GetString()
                                : iface.TryGetProperty("Id", out var id) ? id.GetString()
                                : iface.TryGetProperty("InterfaceGuid", out var ig) ? ig.GetString()
                                : null;
                            if (guid is null) continue;

                            var netsJson = await _xboxService.GetWifiNetworksAsync(guid);
                            Logger.Debug($"WifiNetworks response (guid={guid}): {netsJson}");
                            if (netsJson is null) continue;

                            try
                            {
                                using var ndoc = JsonDocument.Parse(netsJson);
                                var nets = ndoc.RootElement.TryGetProperty("AvailableNetworks", out var av)
                                    ? av
                                    : ndoc.RootElement.TryGetProperty("Networks", out var netsProp)
                                        ? netsProp
                                        : ndoc.RootElement.TryGetProperty("AccessPoints", out var ap)
                                            ? ap
                                            : ndoc.RootElement;

                                if (nets.ValueKind == JsonValueKind.Array)
                                    ParseWifiNetworks(nets);
                                else
                                    Logger.Warn($"WifiNetworks element is not an array: {nets.ValueKind}");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, $"Failed to parse wifi networks for guid={guid}");
                            }
                        }
                    }
                    else
                    {
                        Logger.Warn($"WifiInterfaces response not an array: {ifaces.ValueKind}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to parse wifi interfaces");
                }
            }
            else
            {
                Logger.Warn("GetWifiInterfacesAsync returned null");
            }

        }
        catch (Exception ex)
        {
            StatusMessage = "Network info failed";
            Logger.Error(ex, "RefreshNetworkInfo failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ParseAdapters(JsonElement adapters)
    {
        foreach (var a in adapters.EnumerateArray())
        {
            var na = new NetworkAdapter();

            if (a.TryGetProperty("Description", out var d)) na.Description = d.GetString() ?? "-";
            if (a.TryGetProperty("Type", out var t)) na.TypeName = t.GetString() ?? "-";
            if (a.TryGetProperty("HardwareAddress", out var hw)) na.HardwareAddress = hw.GetString() ?? "-";
            if (a.TryGetProperty("LinkSpeed", out var ls)) na.LinkSpeed = ls.GetString();

            var ips = new System.Text.StringBuilder();
            if (a.TryGetProperty("IpAddresses", out var ipArr))
            {
                foreach (var ip in ipArr.EnumerateArray())
                {
                    var addr = ip.TryGetProperty("IpAddress", out var ipv) ? ipv.GetString() : null;
                    var mask = ip.TryGetProperty("Mask", out var sm) ? sm.GetString() : null;
                    if (!string.IsNullOrEmpty(addr) && addr != "0.0.0.0")
                    {
                        if (ips.Length > 0) ips.AppendLine();
                        ips.Append(addr);
                        if (!string.IsNullOrEmpty(mask) && mask != "0.0.0.0")
                            ips.Append($" / {mask}");
                    }
                }
            }
            na.IpAddressesDisplay = ips.Length > 0 ? ips.ToString() : null;

            var gws = new System.Text.StringBuilder();
            if (a.TryGetProperty("Gateways", out var gwArr))
            {
                foreach (var gw in gwArr.EnumerateArray())
                {
                    var gwAddr = gw.TryGetProperty("IpAddress", out var g) ? g.GetString() : null;
                    if (!string.IsNullOrEmpty(gwAddr) && gwAddr != "0.0.0.0")
                    {
                        if (gws.Length > 0) gws.Append(", ");
                        gws.Append(gwAddr);
                    }
                }
            }
            na.GatewaysDisplay = gws.Length > 0 ? gws.ToString() : null;

            if (a.TryGetProperty("DNSAddresses", out var dnsArr))
            {
                var dns = string.Join(", ", dnsArr.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)));
                na.DnsDisplay = !string.IsNullOrEmpty(dns) ? dns : null;
            }

            if (a.TryGetProperty("DHCP", out var dhcp))
            {
                if (dhcp.TryGetProperty("Address", out var dhcpAddr))
                {
                    var da = dhcpAddr.TryGetProperty("IpAddress", out var dIp) ? dIp.GetString() : null;
                    if (!string.IsNullOrEmpty(da) && da != "0.0.0.0")
                        na.DhcpDisplay = da;
                }
            }

            Adapters.Add(na);
        }
    }

    private void ParseWifiNetworks(JsonElement networks)
    {
        foreach (var n in networks.EnumerateArray().OrderByDescending(n =>
        {
            if (n.TryGetProperty("SignalQuality", out var sq)) return sq.GetInt32();
            return 0;
        }))
        {
            var ssid = n.TryGetProperty("SSID", out var s) ? s.GetString()
                : n.TryGetProperty("Ssid", out var ss) ? ss.GetString()
                : n.TryGetProperty("ssid", out var ss2) ? ss2.GetString()
                : null;
            if (string.IsNullOrEmpty(ssid)) continue;

            var wn = new WifiNetwork
            {
                Ssid = ssid,
                SignalQuality = n.TryGetProperty("SignalQuality", out var sig) ? sig.GetInt32()
                    : n.TryGetProperty("SignalStrength", out var ss3) ? ss3.GetInt32()
                    : 0,
                Authentication = n.TryGetProperty("AuthenticationAlgorithm", out var aa) ? aa.GetString() ?? "-"
                    : n.TryGetProperty("Authentication", out var auth) ? auth.GetString() ?? "-"
                    : "-",
                IsConnected = (n.TryGetProperty("AlreadyConnected", out var ac) && ac.GetBoolean())
                    || (n.TryGetProperty("IsConnected", out var ic) && ic.GetBoolean())
            };

            WifiNetworks.Add(wn);
        }
    }
}
