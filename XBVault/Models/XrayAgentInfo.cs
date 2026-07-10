using System.Text.Json.Serialization;

namespace XBVault.Models;

public class XrayAgentInfo
{
    public int Port { get; set; }
    public string AppName { get; set; } = "";
    public string AppId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Language { get; set; } = "";
    public string Environment { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public List<string> Capabilities { get; set; } = [];
    public bool IsConnected { get; set; }

    public string DisplayName => $"{AppName} v{Version} (port {Port})";
    public string ShortDisplay => $"{AppName} :{Port}";
    public override string ToString() => DisplayName;
}

public class XrayHandshakePayload
{
    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = "";

    [JsonPropertyName("protocol_version")]
    public int ProtocolVersion { get; set; }

    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "";

    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = [];
}

public class XrayHandshake
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("payload")]
    public XrayHandshakePayload? Payload { get; set; }
}

public class XrayLogPayload
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("thread_id")]
    public int? ThreadId { get; set; }
}

public class XrayLogMessage
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("payload")]
    public XrayLogPayload? Payload { get; set; }
}

public class XrayReplPayload
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("script")]
    public string? Script { get; set; }
}

public class XrayReplResultPayload
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class XrayReplResult
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("payload")]
    public XrayReplResultPayload? Payload { get; set; }
}

public class XrayCommandResultPayload
{
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class XrayCommandResult
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("payload")]
    public XrayCommandResultPayload? Payload { get; set; }
}
