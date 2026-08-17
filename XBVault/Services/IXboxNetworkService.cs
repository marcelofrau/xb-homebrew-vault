#nullable enable
using System.Threading.Tasks;

namespace XBVault.Services;

/// <summary>
/// Provides network information queries exposed by the Xbox Device Portal.
/// </summary>
public interface IXboxNetworkService
{
    /// <summary>
    /// Returns raw JSON network adapter configuration.
    /// </summary>
    Task<string?> GetNetworkConfigAsync();

    /// <summary>
    /// Returns raw JSON Wi-Fi interface metadata.
    /// </summary>
    Task<string?> GetWifiInterfacesAsync();

    /// <summary>
    /// Returns raw JSON available networks for a Wi-Fi interface GUID.
    /// </summary>
    Task<string?> GetWifiNetworksAsync(string interfaceGuid);
}
