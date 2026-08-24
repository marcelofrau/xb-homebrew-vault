#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XBVault.Services;

/// <summary>
/// Resolves hosting-service URLs (GoFile, Google Drive, OneDrive) to direct download links.
/// </summary>
public static partial class UrlResolverService
{
    private static readonly HttpClient SharedClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        MaxAutomaticRedirections = 10
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly string[] DirectExtensions =
        [".appx", ".appxbundle", ".msix", ".msixbundle", ".zip", ".xvc", ".eappx", ".eappxbundle", ".emsix", ".emsixbundle"];

    /// <summary>
    /// Returns true if the URL already points to a downloadable package file (has a known extension).
    /// </summary>
    public static bool IsDirectLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var ext = Path.GetExtension(uri.AbsolutePath);
        return Array.Exists(DirectExtensions, e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Attempts to resolve a hosting-service URL to a direct download link.
    /// Returns (resolvedUrl, suggestedFileName).
    /// If the URL is already a direct link, returns it unchanged.
    /// </summary>
    public static async Task<(string Url, string? FileName)> ResolveAsync(string url, CancellationToken ct = default)
    {
        if (IsDirectLink(url))
        {
            Logger.Debug($"UrlResolverService: URL is already direct — {url}");
            return (url, null);
        }

        var uri = new Uri(url);
        var host = uri.Host.ToLowerInvariant();

        try
        {
            if (host.Contains("gofile.io"))
                return await ResolveGoFileAsync(uri, ct);

            if (host.Contains("drive.google.com"))
                return ResolveGoogleDrive(uri);

            if (host.Contains("1drv.ms") || host.Contains("onedrive.live.com") || host.Contains("sharepoint.com"))
                return await ResolveOneDriveAsync(url, ct);

            return await ResolveGenericAsync(url, ct);
        }
        catch (Exception ex)
        {
            Logger.Warn($"UrlResolverService: resolution failed for {host} — {ex.Message}, falling back to original URL");
            return (url, null);
        }
    }

    /// <summary>
    /// GoFile: extract content ID, use API to get download URL.
    /// https://gofile.io/d/{contentId} or https://gofile.io/download/{contentId}
    /// </summary>
    private static async Task<(string Url, string? FileName)> ResolveGoFileAsync(Uri uri, CancellationToken ct)
    {
        Logger.Info($"UrlResolverService: resolving GoFile URL — {uri}");

        // Extract content ID from path
        var path = uri.AbsolutePath.Trim('/');
        string? contentId = null;
        if (path.StartsWith("d/", StringComparison.OrdinalIgnoreCase))
            contentId = path["d/".Length..];
        else if (path.StartsWith("download/", StringComparison.OrdinalIgnoreCase))
            contentId = path["download/".Length..];

        if (string.IsNullOrEmpty(contentId))
            throw new InvalidOperationException("Could not extract GoFile content ID from URL");

        Logger.Debug($"UrlResolverService: GoFile contentId={contentId}");

        // Step 1: Get available server
        var serverResponse = await SharedClient.GetStringAsync("https://api.gofile.io/servers", ct);
        using var serverDoc = JsonDocument.Parse(serverResponse);
        var servers = serverDoc.RootElement.GetProperty("data").GetProperty("servers");
        if (servers.GetArrayLength() == 0)
            throw new InvalidOperationException("No GoFile servers available");
        var server = servers[0].GetProperty("name").GetString();

        // Step 2: Get content info
        var contentUrl = $"https://{server}.gofile.io/contents/{contentId}";
        Logger.Debug($"UrlResolverService: GoFile content API — {contentUrl}");
        var contentResponse = await SharedClient.GetStringAsync(contentUrl, ct);
        using var contentDoc = JsonDocument.Parse(contentResponse);
        var data = contentDoc.RootElement.GetProperty("data");

        // Step 3: Extract download link — files are nested under data.children
        if (data.TryGetProperty("children", out var children) && children.EnumerateObject().MoveNext())
        {
            var firstChild = children.EnumerateObject().First().Value;
            var fileName = firstChild.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            var downloadLink = firstChild.TryGetProperty("link", out var linkProp) ? linkProp.GetString() : null;

            if (!string.IsNullOrEmpty(downloadLink))
            {
                Logger.Info($"UrlResolverService: GoFile resolved — {fileName} → {downloadLink}");
                return (downloadLink, fileName);
            }
        }

        throw new InvalidOperationException("Could not extract download link from GoFile response");
    }

    /// <summary>
    /// Google Drive: extract file ID, construct direct download URL.
    /// https://drive.google.com/file/d/{fileId}/... or https://drive.google.com/open?id={fileId}
    /// </summary>
    private static (string Url, string? FileName) ResolveGoogleDrive(Uri uri)
    {
        Logger.Info($"UrlResolverService: resolving Google Drive URL — {uri}");

        string? fileId = null;

        // Pattern: /file/d/{fileId}/...
        var match = GDriveFileIdRegex().Match(uri.AbsolutePath);
        if (match.Success)
            fileId = match.Groups[1].Value;

        // Pattern: ?id={fileId}
        if (fileId is null)
        {
            foreach (var param in uri.Query.TrimStart('?').Split('&'))
            {
                var kv = param.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "id")
                {
                    fileId = Uri.UnescapeDataString(kv[1]);
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(fileId))
            throw new InvalidOperationException("Could not extract Google Drive file ID from URL");

        var directUrl = $"https://drive.google.com/uc?export=download&id={fileId}";
        Logger.Info($"UrlResolverService: Google Drive resolved — fileId={fileId}");
        return (directUrl, null);
    }

    /// <summary>
    /// OneDrive: follow redirects to get direct download link.
    /// 1drv.ms/*, onedrive.live.com/*, *.sharepoint.com/*
    /// </summary>
    private static async Task<(string Url, string? FileName)> ResolveOneDriveAsync(string url, CancellationToken ct)
    {
        Logger.Info($"UrlResolverService: resolving OneDrive URL — {url}");

        // Try with ?download=1 appended
        var separator = url.Contains('?') ? '&' : '?';
        var downloadUrl = $"{url}{separator}download=1";

        var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.Add("User-Agent", "Mozilla/5.0");

        using var response = await SharedClient.SendAsync(request, ct);

        // If we got a redirect (3xx), the final Location is the direct link
        if (response.StatusCode is >= HttpStatusCode.MovedPermanently and < (HttpStatusCode)400)
        {
            var location = response.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(location))
            {
                Logger.Info($"UrlResolverService: OneDrive resolved via redirect — {location}");
                return (location, null);
            }
        }

        // If the response itself is the file (200 with binary content), use the download URL
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!contentType.Contains("html"))
        {
            Logger.Info($"UrlResolverService: OneDrive resolved — content type {contentType}");
            return (downloadUrl, null);
        }

        // Fallback: return the download URL and let the caller handle it
        Logger.Debug("UrlResolverService: OneDrive redirect did not yield direct link, returning download URL");
        return (downloadUrl, null);
    }

    /// <summary>
    /// Generic: follow redirects, check Content-Type. If it's a binary, return the URL.
    /// If HTML, try to find a download link in the page.
    /// </summary>
    private static async Task<(string Url, string? FileName)> ResolveGenericAsync(string url, CancellationToken ct)
    {
        Logger.Debug($"UrlResolverService: attempting generic resolution — {url}");

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        using var response = await SharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        // If the final URL is different, we resolved via redirect
        var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

        if (!contentType.Contains("html"))
        {
            Logger.Info($"UrlResolverService: generic resolved — type={contentType}, final={finalUrl}");
            return (finalUrl, fileName);
        }

        // HTML page — try to extract a download link
        Logger.Debug("UrlResolverService: generic — got HTML, scanning for download link");
        var html = await response.Content.ReadAsStringAsync(ct);
        var linkMatch = AnchorDownloadRegex().Match(html);
        if (linkMatch.Success)
        {
            var href = linkMatch.Groups[1].Value;
            if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var baseUri = new Uri(finalUrl);
                href = new Uri(baseUri, href).ToString();
            }
            Logger.Info($"UrlResolverService: generic — found download link in HTML: {href}");
            return (href, null);
        }

        Logger.Warn("UrlResolverService: generic — HTML page with no download link found");
        return (url, null);
    }

    [GeneratedRegex(@"/file/d/([^/]+)")]
    private static partial Regex GDriveFileIdRegex();

    [GeneratedRegex("href\\s*=\\s*[\"']([^\"']*download[^\"']*)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorDownloadRegex();
}
