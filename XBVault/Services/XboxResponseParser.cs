using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XBVault.Services;

public static class XboxResponseParser
{
    public static string? TryParseError(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("ErrorMessage", out var msg))
                return msg.GetString();
        }
        catch (Exception ex)
        {
            Logger.Warn($"TryParseError: failed to parse error JSON: {ex.Message}");
        }
        return null;
    }

    public static string? ParseMsixPackageName(string msixPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(msixPath);
            var entry = archive.GetEntry("AppxManifest.xml");
            if (entry is null) return null;

            using var reader = new StreamReader(entry.Open());
            var xml = reader.ReadToEnd();

            var match = Regex.Match(xml, @"<Identity\s[^>]*\bName\s*=\s*""([^""]+)""");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to parse MSIX manifest: {ex.Message}");
            return null;
        }
    }

    public static bool IsIdleCode(HttpStatusCode code) =>
        code == HttpStatusCode.NotFound || code == HttpStatusCode.NoContent;

    public static bool IsSignatureError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Code", out var el) && el.GetInt32() == unchecked((int)0x800B0100))
                return true;
        }
        catch { }
        return false;
    }

    public static bool IsResourceInUseError(string json, out string busyApps)
    {
        busyApps = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("Code", out var el)) return false;
            if (el.GetInt32() != unchecked((int)0x80073D02)) return false;
            busyApps = root.TryGetProperty("Reason", out var r) ? r.GetString() ?? "" : "";
            return true;
        }
        catch { }
        return false;
    }

    public static bool IsHigherVersionError(string json, out string message)
    {
        message = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("Code", out var el)) return false;
            if (el.GetInt32() != unchecked((int)0x80070490)) return false;
            message = root.TryGetProperty("Reason", out var r) ? r.GetString() ?? "" : "";
            return true;
        }
        catch { }
        return false;
    }

    public static bool IsFatalDeploymentError(string json, out string error)
    {
        error = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("Success", out var s) || s.GetBoolean()) return false;
            var code = root.TryGetProperty("Code", out var c) ? c.GetInt32() : 0;
            var reason = root.TryGetProperty("Reason", out var r) ? r.GetString() ?? "" : "";
            error = $"Code={code} {reason}";
            return true;
        }
        catch { }
        return false;
    }

    public static bool IsJsonSuccess(string json, out string statusMessage)
    {
        statusMessage = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Success", out var el) && el.GetBoolean())
            {
                var codeText = root.TryGetProperty("CodeText", out var ct) ? ct.GetString() : "";
                var reason = root.TryGetProperty("Reason", out var r) ? r.GetString() : "";
                statusMessage = $"{reason} {codeText}".Trim();
                return true;
            }

            var errCode = root.TryGetProperty("Code", out var c) ? c.GetInt32() : -1;
            var errText = root.TryGetProperty("CodeText", out var t) ? t.GetString() : "";
            var errReason = root.TryGetProperty("Reason", out var re) ? re.GetString() : "";
            statusMessage = $"Code={errCode} Reason={errReason} CodeText={errText}";
            return false;
        }
        catch (Exception ex)
        {
            statusMessage = $"Parse error: {ex.Message}";
            return false;
        }
    }

    public static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "... (truncated)";

    public static string SizeFormat(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double n = bytes;
        foreach (var u in units)
        {
            if (n < 1024) return $"{n.ToString("F1", CultureInfo.InvariantCulture)}{u}";
            n /= 1024;
        }
        return $"{n.ToString("F1", CultureInfo.InvariantCulture)}TB";
    }
}
