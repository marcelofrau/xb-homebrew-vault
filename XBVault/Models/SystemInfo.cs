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

    [JsonPropertyName("SerialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("XboxLiveDeviceKey")]
    public string? XboxLiveDeviceKey { get; set; }

    [JsonPropertyName("TotalMemory")]
    public string? TotalMemory { get; set; }

    [JsonPropertyName("Cpu")]
    public string? Cpu { get; set; }

    [JsonPropertyName("SystemUptimeMs")]
    public long SystemUptimeMs { get; set; }

    [JsonPropertyName("MacAddress")]
    public string? MacAddress { get; set; }

    [JsonPropertyName("FirmwareVersion")]
    public string? FirmwareVersion { get; set; }

    [JsonPropertyName("XboxHardwareVersion")]
    public string? XboxHardwareVersion { get; set; }

    [JsonIgnore]
    public string? SystemUptimeDisplay
    {
        get
        {
            if (SystemUptimeMs <= 0) return null;
            var ts = TimeSpan.FromMilliseconds(SystemUptimeMs);
            return ts.Days > 0
                ? $"{ts.Days}d {ts.Hours}h {ts.Minutes}m"
                : $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
        }
    }

    [JsonIgnore]
    public string? TotalMemoryDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(TotalMemory)) return null;
            if (long.TryParse(TotalMemory, out var bytes))
            {
                string[] units = ["B", "KB", "MB", "GB"];
                double n = bytes;
                foreach (var u in units)
                {
                    if (n < 1024) return $"{n:F1}{u}";
                    n /= 1024;
                }
                return $"{n:F1}TB";
            }
            return TotalMemory;
        }
    }
}
