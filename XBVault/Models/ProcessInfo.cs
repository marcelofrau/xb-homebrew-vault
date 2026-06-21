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
    public long? MemoryUsage { get; set; }

    [JsonPropertyName("CpuUsage")]
    public double? CpuUsage { get; set; }

    [JsonPropertyName("PageFileUsage")]
    public long PageFileUsage { get; set; }

    [JsonPropertyName("PrivatePageCount")]
    public long PrivatePageCount { get; set; }

    [JsonPropertyName("PackageFullName")]
    public string? PackageFullName { get; set; }

    [JsonPropertyName("PackageFamilyName")]
    public string? PackageFamilyName { get; set; }

    [JsonPropertyName("AppName")]
    public string? AppName { get; set; }

    [JsonIgnore]
    public string MemoryDisplay
    {
        get
        {
            if (MemoryUsage is null or 0) return "-";
            string[] units = ["B", "KB", "MB", "GB"];
            double n = MemoryUsage.Value;
            foreach (var u in units)
            {
                if (n < 1024) return $"{n:F1}{u}";
                n /= 1024;
            }
            return $"{n:F1}TB";
        }
    }

    [JsonIgnore]
    public string CpuDisplay => CpuUsage is null or 0 ? "-" : $"{CpuUsage:F1}%";
}

public enum ProcessSortColumn
{
    ProcessId,
    ImageName,
    MemoryUsage,
    CpuUsage,
    UserName
}

public class ProcessSortState
{
    public ProcessSortColumn Column { get; set; } = ProcessSortColumn.ImageName;
    public bool Ascending { get; set; } = true;
}

public class ProcessListResponse
{
    [JsonPropertyName("Processes")]
    public List<ProcessInfo>? Processes { get; set; }
}
