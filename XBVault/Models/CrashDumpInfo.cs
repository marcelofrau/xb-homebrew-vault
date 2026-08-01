using System.Globalization;
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
                // InvariantCulture: "1.5 GB" regardless of pt-BR comma vs en-US dot
                if (n < 1024) return $"{n.ToString("F1", CultureInfo.InvariantCulture)}{u}";
                n /= 1024;
            }
            return $"{n.ToString("F1", CultureInfo.InvariantCulture)}TB";
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