using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XBVault.Services;

/// <summary>
/// Uploads log files to GoFile for remote sharing.
/// </summary>
public static class LogShareService
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    /// <summary>
    /// Collects all session log files, zips them, uploads to GoFile, returns the download page URL.
    /// </summary>
    public static async Task<string?> ShareAllLogsAsync(
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var logDir = Logger.LogDirectory;
        if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
            return null;

        var logFiles = Directory.GetFiles(logDir, "XBVault-*.log")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .ToArray();

        if (logFiles.Length == 0)
            return null;

        // Concatenate all logs into a single text blob
        var sb = new StringBuilder();
        foreach (var file in logFiles)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"=== {Path.GetFileName(file)} ===");
            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                sb.AppendLine(content);
            }
            catch (IOException) { sb.AppendLine("(could not read)"); }
            catch (UnauthorizedAccessException) { sb.AppendLine("(access denied)"); }
            sb.AppendLine();
        }

        // Zip the concatenated log
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var zipFileName = $"xbvault-logs-{timestamp}.zip";
        var tempZip = Path.Combine(Path.GetTempPath(), zipFileName);

        try
        {
            var logBytes = Encoding.UTF8.GetBytes(sb.ToString());
            using (var zipStream = File.Create(tempZip))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry($"xbvault-logs-{timestamp}.log");
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(logBytes, ct);
            }

            progress?.Report(0.3);

            // Upload to GoFile
            var downloadUrl = await UploadToGoFileAsync(tempZip, zipFileName, progress, ct);
            progress?.Report(1.0);
            return downloadUrl;
        }
        finally
        {
            try { File.Delete(tempZip); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Uploads a single log file (current session) to GoFile.
    /// </summary>
    public static async Task<string?> ShareCurrentLogAsync(
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var logDir = Logger.LogDirectory;
        if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
            return null;

        var latestLog = Directory.GetFiles(logDir, "XBVault-*.log")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latestLog is null)
            return null;

        progress?.Report(0.3);
        var downloadUrl = await UploadToGoFileAsync(latestLog, Path.GetFileName(latestLog), progress, ct);
        progress?.Report(1.0);
        return downloadUrl;
    }

    private static async Task<string?> UploadToGoFileAsync(
        string filePath, string fileName,
        IProgress<double>? progress, CancellationToken ct)
    {
        // Step 1: Get available server
        var server = await GetServerAsync(ct);
        if (server is null) return null;

        progress?.Report(0.5);

        // Step 2: Upload file
        var uploadUrl = $"https://{server}.gofile.io/contents/uploadfile";

        using var fileStream = File.OpenRead(filePath);
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var form = new MultipartFormDataContent();
        form.Add(streamContent, "file", fileName);

        var response = await SharedClient.PostAsync(uploadUrl, form, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        progress?.Report(0.9);

        return ExtractUrlFromResponse(json);
    }

    private static async Task<string?> GetServerAsync(CancellationToken ct)
    {
        try
        {
            var response = await SharedClient.GetAsync("https://api.gofile.io/servers", ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (JsonDocument.Parse(json) is { } doc &&
                doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("servers", out var servers) &&
                servers.GetArrayLength() > 0)
            {
                return servers[0].GetProperty("name").GetString();
            }
        }
        catch (Exception ex) { Logger.Warn($"GoFile server selection failed: {ex.Message}"); }
        return null;
    }

    private static string? ExtractUrlFromResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Primary: { "data": { "downloadPage": "https://..." } }
            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("downloadPage", out var page))
            {
                return page.GetString();
            }

            // Fallback: { "url" } or { "link" }
            if (root.TryGetProperty("url", out var url))
                return url.GetString();
            if (root.TryGetProperty("link", out var link))
                return link.GetString();
        }
        catch (Exception ex) { Logger.Warn($"GoFile response parse failed: {ex.Message}"); }
        return null;
    }
}
