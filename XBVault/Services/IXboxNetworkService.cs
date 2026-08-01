using System.Threading.Tasks;

namespace XBVault.Services;

public interface IXboxNetworkService
{
    Task<string?> GetNetworkConfigAsync();
    Task<string?> GetWifiInterfacesAsync();
    Task<string?> GetWifiNetworksAsync(string interfaceGuid);
}
