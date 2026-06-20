using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public class XboxDeviceService
{
    private HttpClient _http;
    private HttpClientHandler? _handler;
    private bool _configured;
    private bool _connected;
    private string? _csrfToken;
    private string? _baseUrl;
    private string? _username;
    private string? _password;

    public XboxDeviceService()
    {
        _handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            CookieContainer = new CookieContainer()
        };
        _http = new HttpClient(_handler);
    }

    public void Configure(string baseUrl, string username, string password)
    {
        var maskedPw = password.Length > 0 ? $"{password[0]}***" : "";
        Logger.Debug($"XboxDeviceService.Configure({baseUrl}, {username}, {maskedPw})");

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
        var http = new HttpClient(handler);
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
        Logger.Debug("XboxDeviceService configured");
    }

    public bool IsConfigured => _configured;
    public bool IsConnected => _connected;

    public string? GetDevPortalUrl()
    {
        if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_username))
            return null;
        return !string.IsNullOrEmpty(_password)
            ? $"{_baseUrl.Replace("://", $"://{_username}:{_password}@")}"
            : _baseUrl;
    }

    public event Action<bool>? ConnectionChanged;

    public void MarkConnected()
    {
        _connected = true;
        ConnectionChanged?.Invoke(true);
        Logger.Debug("XboxDeviceService marked as connected");
    }

    public void Disconnect()
    {
        Logger.Info("XboxDeviceService.Disconnect");
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
        _http = new HttpClient(_handler);
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
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

    public async Task<List<InstalledPackage>> GetInstalledPackagesAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetInstalledPackages called but not configured");
            return [];
        }

        try
        {
            Logger.Info("GET /api/app/packagemanager/packages");
            var response = await _http.GetAsync("/api/app/packagemanager/packages");
            Logger.Info($"GET /api/app/packagemanager/packages => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
                response.EnsureSuccessStatusCode(); // will throw
            }

            var json = await response.Content.ReadAsStringAsync();
            Logger.Trace($"Packages JSON length: {json.Length} chars");

            using var doc = JsonDocument.Parse(json);
            var sample = doc.RootElement.TryGetProperty("InstalledPackages", out var arr) && arr.GetArrayLength() > 0
                ? arr[0].ToString() : "no packages";
            Logger.Info($"First package raw:\n{sample}");

            var result = JsonSerializer.Deserialize<PackagesResponse>(json);

            var count = result?.InstalledPackages?.Count ?? 0;
            Logger.Debug($"Got {count} installed packages");

            if (result?.InstalledPackages is not null && arr.ValueKind == JsonValueKind.Array)
            {
                for (int i = 0; i < Math.Min(result.InstalledPackages.Count, arr.GetArrayLength()); i++)
                    result.InstalledPackages[i].RawJson = arr[i].ToString();
            }

            return result?.InstalledPackages ?? [];
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetInstalledPackages failed");
            return [];
        }
    }

    public async Task<bool> UninstallPackageAsync(string packageFullName)
    {
        if (!_configured)
        {
            Logger.Warn("Uninstall called but not configured");
            return false;
        }

        try
        {
            Logger.Info($"Uninstalling: {packageFullName}");
            var encoded = Uri.EscapeDataString(packageFullName);
            var url = $"/api/app/packagemanager/package?package={encoded}";
            Logger.Info($"DELETE {url}");
            var response = await DeleteWithCsrfAsync(url);
            Logger.Info($"DELETE => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Uninstall failed for {packageFullName}");
            return false;
        }
    }

    public async Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null)
    {
        var wrapped = progress is not null
            ? new Progress<InstallProgressInfo>(p => progress.Report(p.Total))
            : null;
        return await InstallPackageAsync(filePath, [], wrapped);
    }

    public async Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null)
    {
        if (!_configured)
        {
            Logger.Warn("Install called but not configured");
            return false;
        }
        if (!File.Exists(packagePath))
        {
            Logger.Error($"Install file not found: {packagePath}");
            return false;
        }

        try
        {
            var totalFiles = 1 + dependencies.Length;
            var mainName = Path.GetFileName(packagePath);
            Logger.Info($"Install starting: {mainName} ({dependencies.Length} dependencies)");

            // Upload main package
            progress?.Report(new InstallProgressInfo
            {
                Total = 1.0 / totalFiles * 0,
                Status = $"Uploading {mainName}...",
                CurrentFile = mainName
            });

            var mainOk = await UploadAppxFile(packagePath, progress);
            if (!mainOk)
            {
                Logger.Error($"Main package upload failed: {mainName}");
                return false;
            }

            progress?.Report(new InstallProgressInfo
            {
                Total = 1.0 / totalFiles * 1,
                File = 1,
                Status = $"Uploaded main package",
                CurrentFile = mainName
            });

            // Upload dependencies one at a time
            Logger.Info($"Uploading {dependencies.Length} dependencies...");
            var depIndex = 0;
            foreach (var dep in dependencies)
            {
                depIndex++;
                if (!File.Exists(dep))
                {
                    Logger.Warn($"Dependency not found: {dep}");
                    continue;
                }

                var depName = Path.GetFileName(dep);
                Logger.Info($"  [{depIndex}/{dependencies.Length}] {depName}");
                progress?.Report(new InstallProgressInfo
                {
                    Total = (double)(1 + depIndex) / totalFiles,
                    Status = $"Uploading dependency {depIndex}/{dependencies.Length}: {depName}...",
                    CurrentFile = depName
                });

                await WaitForPackageManagerReady();

                var depOk = await UploadAppxFile(dep, progress);
                if (depOk)
                    Logger.Info($"  Dependency uploaded: {depName}");
                else
                    Logger.Error($"  Dependency failed: {depName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Install failed for {packagePath}");
            return false;
        }
    }

    private async Task<bool> UploadAppxFile(string filePath, IProgress<InstallProgressInfo>? progress = null)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = new FileInfo(filePath).Length;
        Logger.Info($"Uploading: {fileName} ({SizeFormat(fileSize)})");

        for (int attempt = 0; attempt <= 5; attempt++)
        {
            if (attempt > 0)
            {
                var wait = attempt * 5;
                Logger.Info($"Waiting {wait}s (Xbox busy, attempt {attempt}/5)...");
                progress?.Report(new InstallProgressInfo
                {
                    Status = $"Waiting for previous install to finish ({wait}s)...",
                    CurrentFile = fileName
                });
                await Task.Delay(TimeSpan.FromSeconds(wait));
                await WaitForPackageManagerReady();
            }

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            // Build multipart body manually so Content-Type boundary is unquoted
            var boundary = "----XboxUploadBoundary";
            var headerBytes = Encoding.UTF8.GetBytes(
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\n" +
                $"Content-Type: application/octet-stream\r\n\r\n");
            var trailerBytes = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
            var bodyBytes = new byte[headerBytes.Length + fileBytes.Length + trailerBytes.Length];
            headerBytes.CopyTo(bodyBytes, 0);
            fileBytes.CopyTo(bodyBytes, headerBytes.Length);
            trailerBytes.CopyTo(bodyBytes, headerBytes.Length + fileBytes.Length);

            var content = new ByteArrayContent(bodyBytes);
            content.Headers.TryAddWithoutValidation("Content-Type",
                $"multipart/form-data; boundary={boundary}");

            var url = $"/api/appx/packagemanager/package?package={Uri.EscapeDataString(fileName)}";
            Logger.Info($">> POST {url}");
            Logger.Info($"   Content-Type: {content.Headers.ContentType}");
            Logger.Info($"   Content-Length: {content.Headers.ContentLength ?? 0}");
            Logger.Info($"   File: {fileName} ({SizeFormat(fileSize)})");

            progress?.Report(new InstallProgressInfo
            {
                Status = $"Uploading {fileName}...",
                CurrentFile = fileName,
                File = 0.3
            });

            var response = await PostWithCsrfAsync(url, content);

            Logger.Info($"<< {response.StatusCode:D} ({response.ReasonPhrase})");
            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadResponseBody(response);
                Logger.Warn($"   Body: {body}");
            }
            else
            {
                var body = await ReadResponseBody(response);
                Logger.Info($"   Response: {body}");
            }

            progress?.Report(new InstallProgressInfo
            {
                Status = response.IsSuccessStatusCode
                    ? $"Uploaded {fileName} ✓"
                    : $"Upload failed: {fileName}",
                CurrentFile = fileName,
                File = response.IsSuccessStatusCode ? 1.0 : 0
            });

            if (response.StatusCode != System.Net.HttpStatusCode.Conflict)
                return response.IsSuccessStatusCode;
        }

        return false;
    }

    private async Task WaitForPackageManagerReady()
    {
        Logger.Info("Waiting for package manager to be ready...");
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await _http.GetAsync("/api/app/packagemanager/state");
                var code = (int)resp.StatusCode;
                Logger.Info($"GET /api/app/packagemanager/state => {code}");
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 means no operation in progress
                    await Task.Delay(2000);
                    var resp2 = await _http.GetAsync("/api/app/packagemanager/state");
                    var code2 = (int)resp2.StatusCode;
                    Logger.Info($"GET /api/app/packagemanager/state => {code2}");
                    if (resp2.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Logger.Info("Package manager ready (got 404 twice)");
                        return;
                    }
                }
            }
            catch
            {
                // Ignore errors during polling
            }
            await Task.Delay(3000);
        }
        Logger.Warn("Timed out waiting for package manager, continuing anyway");
    }

    private async Task<string> ReadResponseBody(HttpResponseMessage resp)
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

    private async Task<HttpResponseMessage> PostWithCsrfAsync(string url, HttpContent? content)
    {
        await EnsureCsrfTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (!string.IsNullOrEmpty(_csrfToken))
            req.Headers.Add("X-CSRF-Token", _csrfToken);
        return await _http.SendAsync(req);
    }

    private async Task<HttpResponseMessage> DeleteWithCsrfAsync(string url)
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

    private static string SizeFormat(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double n = bytes;
        foreach (var u in units)
        {
            if (n < 1024) return $"{n:F1}{u}";
            n /= 1024;
        }
        return $"{n:F1}TB";
    }

    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        if (!_configured)
        {
            Logger.Warn("CaptureScreenshot called but not configured");
            return null;
        }

        try
        {
            var url = $"/ext/screenshot?download=true&hdr=false&time={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            Logger.Info($"GET {url}");
            var response = await _http.GetAsync(url);
            Logger.Info($"GET screenshot => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "CaptureScreenshot failed");
            return null;
        }
    }

    public async Task<string?> GetSystemInfoAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetSystemInfo called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/systeminfo");
            var response = await _http.GetAsync("/api/systeminfo");
            Logger.Info($"GET /api/systeminfo => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/systeminfo failed: {await ReadResponseBody(response)}");

            Logger.Info("GET /api/os/info (fallback)");
            var fallback = await _http.GetAsync("/api/os/info");
            Logger.Info($"GET /api/os/info => {(int)fallback.StatusCode}");
            if (fallback.IsSuccessStatusCode)
                return await fallback.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/os/info also failed: {await ReadResponseBody(fallback)}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetSystemInfo failed");
            return null;
        }
    }

    public async Task<string?> GetProcessesAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetProcesses called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/resourcemanager/processes");
            var response = await _http.GetAsync("/api/resourcemanager/processes");
            Logger.Info($"GET /api/resourcemanager/processes => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetProcesses failed");
            return null;
        }
    }

    public async Task<bool> KillProcessAsync(int pid)
    {
        if (!_configured)
        {
            Logger.Warn("KillProcess called but not configured");
            return false;
        }

        try
        {
            Logger.Info($"DELETE /api/resourcemanager/process?pid={pid}");
            var response = await _http.DeleteAsync($"/api/resourcemanager/process?pid={pid}");
            Logger.Info($"DELETE process => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "KillProcess failed");
            return false;
        }
    }

    public async Task<bool> RestartXboxAsync()
    {
        if (!_configured)
        {
            Logger.Warn("RestartXbox called but not configured");
            return false;
        }

        try
        {
            Logger.Info("POST /api/control/restart");
            var response = await PostWithCsrfAsync("/api/control/restart", null);
            Logger.Info($"POST /api/control/restart => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "RestartXbox failed");
            return false;
        }
    }

    public async Task<bool> ShutdownXboxAsync()
    {
        if (!_configured)
        {
            Logger.Warn("ShutdownXbox called but not configured");
            return false;
        }

        try
        {
            Logger.Info("POST /api/control/shutdown");
            var response = await PostWithCsrfAsync("/api/control/shutdown", null);
            Logger.Info($"POST /api/control/shutdown => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ShutdownXbox failed");
            return false;
        }
    }

    public async Task<string?> GetWifiNetworksAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetWifiNetworks called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/networking/wifi/networks");
            var response = await _http.GetAsync("/api/networking/wifi/networks");
            Logger.Info($"GET /api/networking/wifi/networks => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetWifiNetworks failed");
            return null;
        }
    }

    public async Task<string?> GetNetworkProfilesAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetNetworkProfiles called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/networking/networkprofiles");
            var response = await _http.GetAsync("/api/networking/networkprofiles");
            Logger.Info($"GET /api/networking/networkprofiles => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            var body = await ReadResponseBody(response);
            Logger.Warn($"GET /api/networking/networkprofiles failed ({(int)response.StatusCode}): {body}");

            Logger.Info("GET /api/networking/wifi/networks (fallback)");
            var fallback = await _http.GetAsync("/api/networking/wifi/networks");
            Logger.Info($"GET /api/networking/wifi/networks => {(int)fallback.StatusCode}");
            if (fallback.IsSuccessStatusCode)
                return await fallback.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/networking/wifi/networks also failed: {await ReadResponseBody(fallback)}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetNetworkProfiles failed");
            return null;
        }
    }

    private string GetWsBaseUrl()
    {
        var http = _http.BaseAddress?.ToString() ?? "";
        return http.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');
    }

    public async Task ConnectPerformanceWsAsync(Action<PerformanceSnapshot> onData, CancellationToken ct)
    {
        if (!_configured)
        {
            Logger.Warn("ConnectPerformanceWs called but not configured");
            return;
        }

        var ws = new ClientWebSocket();
        try
        {
            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is not null)
                ws.Options.SetRequestHeader("Authorization", $"{auth.Scheme} {auth.Parameter}");

            if (!string.IsNullOrEmpty(_csrfToken))
                ws.Options.SetRequestHeader("Cookie", $"CSRF-Token={_csrfToken}");

            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

            var wsUrl = $"{GetWsBaseUrl()}/api/resourcemanager/systemperf";
            Logger.Info($"WS connecting to {wsUrl}");
            await ws.ConnectAsync(new Uri(wsUrl), ct);
            Logger.Info("WS connected");

            var buffer = new byte[8192];
            var messageBuf = new System.Text.StringBuilder();

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                messageBuf.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var json = messageBuf.ToString();
                    messageBuf.Clear();

                    var snap = PerformanceSnapshot.Parse(json);
                    if (snap is not null)
                        onData(snap);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("WS cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "WS error");
        }
        finally
        {
            if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None); }
                catch { }
            }
            ws.Dispose();
            Logger.Info("WS disconnected");
        }
    }
}

internal class PackagesResponse
{
    public List<InstalledPackage> InstalledPackages { get; set; } = [];
}

public class ConnectionTestResult
{
    public bool Success { get; }
    public int? StatusCode { get; }
    public string? ErrorDetail { get; }
    public bool IsCancelled { get; }

    public ConnectionTestResult(bool success, int? statusCode, string? errorDetail, bool isCancelled = false)
    {
        Success = success;
        StatusCode = statusCode;
        ErrorDetail = errorDetail;
        IsCancelled = isCancelled;
    }
}
