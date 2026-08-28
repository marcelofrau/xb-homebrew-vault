#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XBVault.Models;

public class ConsoleInfo
{
    [JsonPropertyName("OsVersion")]
    public string? OsVersion { get; set; }

    [JsonPropertyName("DevMode")]
    public string? DevMode { get; set; }

    [JsonPropertyName("OsEdition")]
    public string? OsEdition { get; set; }

    [JsonPropertyName("ConsoleType")]
    public string? ConsoleType { get; set; }

    [JsonPropertyName("ConsoleId")]
    public string? ConsoleId { get; set; }

    [JsonPropertyName("DeviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("SerialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("DevkitCertificateExpirationTime")]
    public long DevkitCertificateExpirationTime { get; set; }

    [JsonIgnore]
    public DateTimeOffset? DevkitCertExpiration => DevkitCertificateExpirationTime > 0
        ? DateTimeOffset.FromUnixTimeSeconds(DevkitCertificateExpirationTime)
        : null;
}

public class MachineNameInfo
{
    [JsonPropertyName("ComputerName")]
    public string? ComputerName { get; set; }
}

public class XboxSettingsResponse
{
    [JsonPropertyName("Settings")]
    public List<XboxSetting> Settings { get; set; } = [];
}

public class XboxSetting
{
    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Value")]
    public string? Value { get; set; }

    [JsonPropertyName("Category")]
    public string? Category { get; set; }

    [JsonPropertyName("Type")]
    public string? Type { get; set; }
}