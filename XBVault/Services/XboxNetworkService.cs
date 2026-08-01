using System;
using System.Threading.Tasks;

namespace XBVault.Services;

public class XboxNetworkService : IXboxNetworkService
{
    private readonly XboxAuthService _auth;

    public XboxNetworkService(XboxAuthService auth)
    {
        _auth = auth;
    }

    public async Task<string?> GetNetworkConfigAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetNetworkConfig called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/networking/ipconfig");
            var response = await _auth.Http.GetAsync("/api/networking/ipconfig");
            Logger.Info($"GET /api/networking/ipconfig => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetNetworkConfig failed");
            return null;
        }
    }

    public async Task<string?> GetWifiInterfacesAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetWifiInterfaces called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/wifi/interfaces");
            var response = await _auth.Http.GetAsync("/api/wifi/interfaces");
            Logger.Info($"GET /api/wifi/interfaces => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetWifiInterfaces failed");
            return null;
        }
    }

    public async Task<string?> GetWifiNetworksAsync(string interfaceGuid)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetWifiNetworks called but not configured");
            return null;
        }

        try
        {
            var path = $"/api/wifi/networks?interface={interfaceGuid}";
            Logger.Info($"GET {path}");
            var response = await _auth.Http.GetAsync(path);
            Logger.Info($"GET {path} => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetWifiNetworks failed");
            return null;
        }
    }
}
