using System.Text.Json.Serialization;

namespace XBVault.Models;

public class SystemInfo
{
    [JsonPropertyName("ConsoleType")]
    public string? ConsoleType { get; set; }

    [JsonPropertyName("OsVersion")]
    public string? OsVersion { get; set; }

    [JsonPropertyName("OsEdition")]
    public string? OsEdition { get; set; }

    [JsonPropertyName("DeviceName")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("Platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("Region")]
    public string? Region { get; set; }

    [JsonPropertyName("Language")]
    public string? Language { get; set; }
}
