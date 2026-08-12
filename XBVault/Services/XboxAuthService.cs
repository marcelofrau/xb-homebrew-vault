using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

#pragma warning disable CA1001 // HttpClient+Handler are long-lived singleton
#pragma warning disable CA5359 // Xbox uses self-signed certs — bypass intentional

namespace XBVault.Services;

public class XboxAuthService : IXboxAuthService
{
    private HttpClient _http;
    private HttpClientHandler? _handler;
    private bool _configured;
    private bool _connected;
    private bool _userDisconnected;
    private DateTime? _lastAutoConnectFailAt;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private string? _csrfToken;
    private string? _baseUrl;
    private string? _username;
    private string? _password;
    private string? _smbPassword;

    public event Action<bool>? ConnectionChanged;

    public XboxAuthService()
    {
        _handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            CookieContainer = new CookieContainer()
        };
        _http = new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Configure(string baseUrl, string username, string password)
    {
        var maskedPw = password.Length > 0 ? $"{password[0]}***" : "";
        Logger.Debug($"XboxAuthService.Configure({baseUrl}, {username}, {maskedPw})");

        var auth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));

        _baseUrl = baseUrl;
        _username = username;
        _password = password;

        // Fresh client each call — BaseAddress freezes after first request
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            CookieContainer = new CookieContainer()
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
        http.BaseAddress = new Uri(baseUrl);

        var oldHttp = _http;
        var oldHandler = _handler;
        _http = http;
        _handler = handler;
        _csrfToken = null;
        oldHttp.Dispose();
        oldHandler?.Dispose();
        _configured = true;
        Logger.Debug("XboxAuthService configured");
    }

    public bool IsConfigured => _configured;
    public bool IsConnected => _connected;
    public string? SmbPassword => _smbPassword;
    public string? Host => _baseUrl is not null ? new Uri(_baseUrl).Host : null;

    public SshConnectionInfo GetSshCredentials()
    {
        if (string.IsNullOrEmpty(_baseUrl))
            throw new InvalidOperationException("Xbox not configured");

        var pw = _smbPassword ?? _password;
        if (string.IsNullOrEmpty(pw))
            throw new InvalidOperationException("No password available");

        var uri = new Uri(_baseUrl);
        Logger.Debug($"GetSshCredentials: host={uri.Host}, user=DevToolsUser, hasSmbPw={_smbPassword is not null}");
        return new SshConnectionInfo(uri.Host, 22, "DevToolsUser", pw);
    }

    public async Task<string?> FetchSmbPasswordAsync()
    {
        try
        {
            var response = await _http.GetAsync("/ext/smb/developerfolder");
            var body = await response.Content.ReadAsStringAsync();
            Logger.Debug($"SMB endpoint returned: {response.StatusCode}");
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(body);
            var pw = doc.RootElement.GetProperty("Password").GetString();
            _smbPassword = pw;
            Logger.Debug("SMB password fetched successfully");
            return pw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch SMB password");
            return null;
        }
    }

    public string? GetDevPortalUrl()
    {
        if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_username))
            return null;
        return !string.IsNullOrEmpty(_password)
            ? $"{_baseUrl.Replace("://", $"://{_username}:{_password}@")}"
            : _baseUrl;
    }

    public void MarkConnected()
    {
        _connected = true;
        _userDisconnected = false;
        ConnectionChanged?.Invoke(true);
        Logger.Debug("XboxAuthService marked as connected");
    }

    public void Disconnect()
    {
        Logger.Info("XboxAuthService.Disconnect");
        _userDisconnected = true;
        _configured = false;
        _connected = false;
        ConnectionChanged?.Invoke(false);
        _csrfToken = null;
        _http.Dispose();
        _handler?.Dispose();
        _handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            CookieContainer = new CookieContainer()
        };
        _http = new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public virtual async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        if (!_configured)
        {
            Logger.Warn("TestConnection called but not configured");
            return new ConnectionTestResult(false, null, null);
        }

        try
        {
            Logger.Info("GET /api/os/info");
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var response = await _http.GetAsync("/api/os/info", linkedCts.Token);
            Logger.Info($"GET /api/os/info => {(int)response.StatusCode}");

            if (response.IsSuccessStatusCode)
                await ExtractCsrfTokenAsync();

            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
            return new ConnectionTestResult(
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Logger.Info("Connection test cancelled by user");
            return new ConnectionTestResult(false, null, "User cancelled", isCancelled: true);
        }
        catch (OperationCanceledException)
        {
            Logger.Error("Connection test timed out");
            return new ConnectionTestResult(false, null, "Connection timed out");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException se)
        {
            Logger.Error(ex, "Connection test failed (socket)");
            var detail = se.SocketErrorCode switch
            {
                System.Net.Sockets.SocketError.ConnectionRefused => "Connection refused",
                System.Net.Sockets.SocketError.HostUnreachable => "Host unreachable",
                System.Net.Sockets.SocketError.NetworkUnreachable => "Network unreachable",
                System.Net.Sockets.SocketError.HostNotFound => "DNS resolution failed",
                _ => $"Socket error {se.SocketErrorCode}"
            };
            return new ConnectionTestResult(false, null, detail);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error(ex, "Connection test failed (HTTP)");
            return new ConnectionTestResult(false, null, ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Connection test failed");
            return new ConnectionTestResult(false, null, ex.Message);
        }
    }

    private static readonly TimeSpan AutoConnectCooldown = TimeSpan.FromSeconds(30);

    public async Task<bool> EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_connected) return true;
        if (!_configured || _userDisconnected || !SettingsService.Current.AutoConnect)
            return false;
        if (IsAutoConnectCooldownActive())
            return false;

        await _connectLock.WaitAsync(ct);
        try
        {
            // Re-check after acquiring the lock — another caller may have connected meanwhile
            if (_connected) return true;
            if (!_configured || _userDisconnected || !SettingsService.Current.AutoConnect)
                return false;
            if (IsAutoConnectCooldownActive())
                return false;

            if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
                return false;

            Configure(_baseUrl, _username, _password);
            var result = await TestConnectionAsync(ct);
            if (result.Success)
            {
                MarkConnected();
                Logger.Info("Auto-connect succeeded");
                return true;
            }

            _lastAutoConnectFailAt = DateTime.UtcNow;
            Logger.Info($"Auto-connect failed: {result.ErrorDetail ?? "unknown reason"}");
            return false;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private bool IsAutoConnectCooldownActive()
        => _lastAutoConnectFailAt is { } last && DateTime.UtcNow - last < AutoConnectCooldown;

    internal HttpClient Http => _http;
    internal string? CsrfToken => _csrfToken;
    internal string? BaseUrl => _baseUrl;
    internal string GetWsBaseUrl()
    {
        var http = _http.BaseAddress?.ToString() ?? "";
        return http.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');
    }

    internal async Task<string> ReadResponseBody(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body)) return "(empty body)";
            return body.Length <= 2000 ? body : body[..2000] + "... (truncated)";
        }
        catch
        {
            return "(unreadable body)";
        }
    }

    internal async Task<HttpResponseMessage> PostWithCsrfAsync(string url, HttpContent? content)
    {
        await EnsureCsrfTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (!string.IsNullOrEmpty(_csrfToken))
            req.Headers.Add("X-CSRF-Token", _csrfToken);
        return await _http.SendAsync(req);
    }

    internal async Task<HttpResponseMessage> DeleteWithCsrfAsync(string url)
    {
        await EnsureCsrfTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        if (!string.IsNullOrEmpty(_csrfToken))
            req.Headers.Add("X-CSRF-Token", _csrfToken);
        return await _http.SendAsync(req);
    }

    private async Task EnsureCsrfTokenAsync()
    {
        if (!string.IsNullOrEmpty(_csrfToken))
            return;

        await TryFetchCsrfFrom("/api/os/info");
        if (string.IsNullOrEmpty(_csrfToken))
            await TryFetchCsrfFrom("/");
    }

    private async Task TryFetchCsrfFrom(string path)
    {
        Logger.Info($"Fetching CSRF from {path}");
        try
        {
            var resp = await _http.GetAsync(path);
            Logger.Info($"GET {path} => {(int)resp.StatusCode}");

            Logger.Info("--- Response headers ---");
            foreach (var h in resp.Headers)
                Logger.Info($"  {h.Key}: {string.Join(", ", h.Value)}");
            foreach (var h in resp.Content.Headers)
                Logger.Info($"  Content-{h.Key}: {string.Join(", ", h.Value)}");

            var body = await resp.Content.ReadAsStringAsync();
            if (body.Length > 0)
                Logger.Info($"--- Response body (first 1000) ---\n{(body.Length > 1000 ? body[..1000] : body)}");

            if (resp.IsSuccessStatusCode)
                await ExtractCsrfTokenAsync();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to fetch CSRF from {path}: {ex.Message}");
        }
    }

    private Task ExtractCsrfTokenAsync()
    {
        var baseAddress = _http.BaseAddress;
        if (baseAddress is null)
        {
            Logger.Warn("No BaseAddress set, cannot extract CSRF");
            return Task.CompletedTask;
        }

        try
        {
            var container = _handler?.CookieContainer;
            if (container is null)
            {
                Logger.Warn("No CookieContainer configured");
                return Task.CompletedTask;
            }

            var cookies = container.GetCookies(baseAddress);
            foreach (System.Net.Cookie c in cookies)
                Logger.Info($"  Cookie: {c.Name}={c.Value}");

            var token = cookies["CSRF-Token"]?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                _csrfToken = token;
                _http.DefaultRequestHeaders.Remove("X-CSRF-Token");
                _http.DefaultRequestHeaders.Add("X-CSRF-Token", _csrfToken);
                Logger.Info($"CSRF token extracted ({_csrfToken.Length} chars)");
            }
            else
            {
                Logger.Warn("No CSRF-Token cookie found");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"CSRF extraction error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _http.Dispose();
        _handler?.Dispose();
        GC.SuppressFinalize(this);
    }
}
