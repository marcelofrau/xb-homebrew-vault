using System.Text.Json.Serialization;

namespace XBVault.Models;

public class CrashDumpInfo
{
    [JsonPropertyName("FileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("FileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("CreatedAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonIgnore]
    public string FileSizeDisplay
    {
        get
        {
            string[] units = ["B", "KB", "MB", "GB"];
            double n = FileSize;
            foreach (var u in units)
            {
                if (n < 1024) return $"{n:F1}{u}";
                n /= 1024;
            }
            return $"{n:F1}TB";
        }
    }

    [JsonIgnore]
    public string CreatedAtDisplay => CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
}

public class CrashDumpListResponse
{
    [JsonPropertyName("CrashDumps")]
    public List<CrashDumpInfo>? CrashDumps { get; set; }
}

public class CrashControlInfo
{
    [JsonPropertyName("CrashDumpEnabled")]
    public bool CrashDumpEnabled { get; set; }
}