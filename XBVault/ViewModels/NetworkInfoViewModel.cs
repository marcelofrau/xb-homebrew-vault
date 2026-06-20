using System;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private string? _networkInfoText;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            var profilesJson = await _xboxService.GetNetworkProfilesAsync();
            if (profilesJson is null)
            {
                StatusMessage = "Failed to get network profiles";
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(profilesJson);
                var sb = new System.Text.StringBuilder();

                if (doc.RootElement.TryGetProperty("NetworkProfiles", out var profiles))
                {
                    foreach (var p in profiles.EnumerateArray())
                    {
                        var name = p.TryGetProperty("Name", out var n) ? n.GetString() : "-";
                        var desc = p.TryGetProperty("Description", out var d) ? d.GetString() : "-";
                        var ip = p.TryGetProperty("IpAddress", out var ipv) ? ipv.GetString() : "-";
                        var mask = p.TryGetProperty("SubnetMask", out var sm) ? sm.GetString() : "-";
                        var gw = p.TryGetProperty("Gateway", out var g) ? g.GetString() : "-";
                        var mac = p.TryGetProperty("MacAddress", out var m) ? m.GetString() : "-";
                        var dns = p.TryGetProperty("DnsAddresses", out var dnsArr)
                            ? string.Join(", ", dnsArr.EnumerateArray().Select(x => x.GetString()))
                            : "-";

                        sb.AppendLine($"{name} ({desc})");
                        sb.AppendLine($"  IP:        {ip}");
                        sb.AppendLine($"  Mask:      {mask}");
                        sb.AppendLine($"  Gateway:   {gw}");
                        sb.AppendLine($"  MAC:       {mac}");
                        sb.AppendLine($"  DNS:       {dns}");
                        sb.AppendLine();
                    }
                }

                NetworkInfoText = sb.Length > 0 ? sb.ToString().TrimEnd() : "No network profiles found";
            }
            catch
            {
                NetworkInfoText = profilesJson;
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
}
