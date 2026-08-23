#nullable enable
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XBVault.Services;

public sealed class GitHubReleaseCheckerService : IDisposable
{
    private const string ApiUrl = "https://api.github.com/repos/marcelofrau/xb-homebrew-vault/releases/latest";
    private static readonly HttpClient _defaultHttp = new();
    private static readonly Version ZeroVersion = new(0, 0, 0);

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public GitHubReleaseCheckerService(HttpClient? http = null)
    {
        _http = http ?? _defaultHttp;
        _ownsHttp = http is null;
    }

    public sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }

    public async Task<GitHubRelease?> CheckLatestReleaseAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
            req.Headers.UserAgent.ParseAdd("XBVault/1.0");
            using var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }
        catch (Exception ex)
        {
            Logger.Warn($"GitHub release check failed: {ex.Message}");
            return null;
        }
    }

    public static bool IsNewerVersion(string? tagName, string currentVersion)
    {
        if (string.IsNullOrEmpty(tagName)) return false;
        var tag = tagName.TrimStart('v', 'V');
        var current = ParseVersion(currentVersion) ?? ZeroVersion;
        var latest = ParseVersion(tag);
        return latest is not null && latest > current;
    }

    private static Version? ParseVersion(string version)
    {
        // strip git hash after + and any non-numeric suffix after -
        var plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];
        var dash = version.IndexOf('-');
        if (dash >= 0) version = version[..dash];
        return Version.TryParse(version, out var v) ? v : null;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
