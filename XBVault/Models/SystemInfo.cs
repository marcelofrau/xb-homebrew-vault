#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace XBVault.Models;

public enum SystemInfoRowBadge
{
    None,
    Highlight,
    Positive,
    Negative
}

public class SystemInfoRow
{
    public SystemInfoRow(string label, string value, SystemInfoRowBadge badge)
    {
        Label = label;
        Value = value;
        Badge = badge;
    }

    public string Label { get; }

    public string Value { get; }

    public SystemInfoRowBadge Badge { get; }

    public bool IsHighlight => Badge == SystemInfoRowBadge.Highlight;

    public bool IsPositive => Badge == SystemInfoRowBadge.Positive;

    public bool IsNegative => Badge == SystemInfoRowBadge.Negative;
}

public class SystemInfoCard
{
    private static readonly Dictionary<string, Bitmap> _iconCache = [];
    private readonly string _iconSource;
    private Bitmap? _icon;

    public SystemInfoCard(string title, string iconSource, IReadOnlyList<SystemInfoRow> rows)
    {
        Title = title;
        _iconSource = iconSource;
        Rows = rows;
    }

    public string Title { get; }

    public Bitmap? Icon => _icon ??= LoadIcon(_iconSource);

    public IReadOnlyList<SystemInfoRow> Rows { get; }

    private static Bitmap LoadIcon(string uri)
    {
        if (_iconCache.TryGetValue(uri, out var cached)) return cached;
        using var stream = AssetLoader.Open(new Uri(uri));
        var bitmap = new Bitmap(stream);
        _iconCache[uri] = bitmap;
        return bitmap;
    }
}

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
                    // InvariantCulture: "1.5 GB" regardless of pt-BR comma vs en-US dot
                    if (n < 1024) return $"{n.ToString("F1", CultureInfo.InvariantCulture)}{u}";
                    n /= 1024;
                }
                return $"{n.ToString("F1", CultureInfo.InvariantCulture)}TB";
            }
            return TotalMemory;
        }
    }
}
