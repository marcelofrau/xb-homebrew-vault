using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XBVault.Models;

public class VersionToStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();

        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var major = root.TryGetProperty("Major", out var m) ? m.GetInt32() : 0;
            var minor = root.TryGetProperty("Minor", out var n) ? n.GetInt32() : 0;
            var build = root.TryGetProperty("Build", out var b) ? b.GetInt32() : 0;
            var rev = root.TryGetProperty("Revision", out var r) ? r.GetInt32() : 0;
            return $"{major}.{minor}.{build}.{rev}";
        }

        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public class InstalledPackage : INotifyPropertyChanged
{
    private bool _isUninstalling;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("PackageFullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("PackageDisplayName")]
    public string? DisplayName { get; set; }

    [JsonConverter(typeof(VersionToStringConverter))]
    public string? Version { get; set; }

    public string? Publisher { get; set; }

    public bool CanUninstall { get; set; }

    [JsonPropertyName("PackageOrigin")]
    public int Origin { get; set; }

    [JsonPropertyName("PackageFamilyName")]
    public string? PackageFamilyName { get; set; }

    [JsonIgnore]
    public string? DisplayPublisher
    {
        get
        {
            if (string.IsNullOrEmpty(Publisher)) return null;
            var p = Publisher;
            var idx = p.LastIndexOf("CN=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                p = p[(idx + 3)..];
            idx = p.IndexOf(",", StringComparison.Ordinal);
            if (idx >= 0)
                p = p[..idx];
            return p.Trim();
        }
    }

    [JsonIgnore]
    public string DisplayOrigin
    {
        get => Origin switch
        {
            0 => "Unknown",
            1 => "Store",
            2 => "System",
            3 => "Developer",
            4 => "Bundle",
            5 => "Sideload",
            _ => $"Origin_{Origin}"
        };
    }

    [JsonIgnore]
    public string? RawJson { get; set; }

    [JsonIgnore]
    public bool IsUninstalling
    {
        get => _isUninstalling;
        set
        {
            if (_isUninstalling == value) return;
            _isUninstalling = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
