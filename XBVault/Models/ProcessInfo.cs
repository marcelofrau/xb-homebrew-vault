using System.Text.Json.Serialization;

namespace XBVault.Models;

public class ProcessInfo
{
    [JsonPropertyName("ProcessId")]
    public int ProcessId { get; set; }

    [JsonPropertyName("ImageName")]
    public string? ImageName { get; set; }

    [JsonPropertyName("UserName")]
    public string? UserName { get; set; }

    [JsonPropertyName("MemoryUsage")]
    public long MemoryUsage { get; set; }

    [JsonPropertyName("CpuUsage")]
    public double CpuUsage { get; set; }

    [JsonPropertyName("PageFileUsage")]
    public long PageFileUsage { get; set; }

    [JsonPropertyName("PrivatePageCount")]
    public long PrivatePageCount { get; set; }

    [JsonIgnore]
    public string MemoryDisplay => FormatBytes(MemoryUsage);

    private static string FormatBytes(long bytes)
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
}

public class ProcessListResponse
{
    [JsonPropertyName("Processes")]
    public List<ProcessInfo>? Processes { get; set; }
}
