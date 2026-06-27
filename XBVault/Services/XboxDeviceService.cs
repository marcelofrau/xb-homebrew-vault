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

#pragma warning disable CA1001 // HttpClient+Handler are long-lived singleton
#pragma warning disable CA5359 // Xbox uses self-signed certs — bypass intentional

namespace XBVault.Services;

public class XboxDeviceService
{
    private const int PollDelayMs = 2000;
    private const int RetryDelayMs = 3000;

    private HttpClient _http;
    private HttpClientHandler? _handler;
    private bool _configured;
    private bool _connected;
    private string? _csrfToken;
    private string? _baseUrl;
    private string? _username;
    private string? _password;
    private string? _smbPassword;

    public XboxDeviceService()
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
        Logger.Debug("XboxDeviceService configured");
    }

    public bool IsConfigured => _configured;
    public bool IsConnected => _connected;
    public string? SmbPassword => _smbPassword;

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
        _http = new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(30) };
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

    public async Task<(bool Success, string? ErrorMessage)> LaunchPackageAsync(string packageFullName, string packageRelativeId)
    {
        if (!_configured)
        {
            Logger.Warn("Launch called but not configured");
            return (false, null);
        }

        try
        {
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(packageRelativeId));
            var encoded = Uri.EscapeDataString(b64);
            var url = $"/api/taskmanager/app?appid={encoded}";
            Logger.Info($"Launching: {packageRelativeId}");
            var response = await PostWithCsrfAsync(url, new StringContent(""));
            Logger.Info($"POST {url} => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return (true, null);

            var body = await ReadResponseBody(response);
            Logger.Warn($"Body: {body}");
            var msg = TryParseError(body) ?? $"HTTP {(int)response.StatusCode}";
            return (false, msg);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Launch failed for {packageRelativeId}");
            return (false, "Request failed");
        }
    }

    public async Task<HashSet<string>> GetRunningPackageNamesAsync()
    {
        if (!_configured) return [];

        try
        {
            Logger.Info("GET /api/resourcemanager/processes (for running packages)");
            var response = await _http.GetAsync("/api/resourcemanager/processes");
            Logger.Info($"GET /api/resourcemanager/processes => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<ProcessListResponse>(json);
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (parsed?.Processes is not null)
            {
                foreach (var p in parsed.Processes)
                {
                    if (!string.IsNullOrEmpty(p.PackageFullName))
                        running.Add(p.PackageFullName);
                    if (!string.IsNullOrEmpty(p.PackageFamilyName))
                        running.Add(p.PackageFamilyName);
                }
            }

            Logger.Info($"Processes with package info: {running.Count}");
            return running;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetRunningPackageNames failed");
            return [];
        }
    }

    public async Task<string?> GetRunningTitleAsync()
    {
        if (!_configured) return null;

        try
        {
            Logger.Info("GET /ext/app/runningtitle");
            var response = await _http.GetAsync("/ext/app/runningtitle");
            Logger.Info($"GET /ext/app/runningtitle => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var pfn = doc.RootElement.TryGetProperty("PackageFullName", out var p) ? p.GetString() : null;
            Logger.Info($"Running title: {(string.IsNullOrEmpty(pfn) ? "(none)" : pfn)}");
            return string.IsNullOrEmpty(pfn) ? null : pfn;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetRunningTitle failed");
            return null;
        }
    }

    public async Task<bool> SuspendPackageAsync(string packageFullName)
    {
        if (!_configured) return false;
        try
        {
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(packageFullName));
            var encoded = Uri.EscapeDataString(b64);
            var url = $"/api/taskmanager/app/state?package={encoded}&state=suspend";
            Logger.Info($"Suspend: {packageFullName}");
            var response = await PostWithCsrfAsync(url, new StringContent(""));
            Logger.Info($"POST {url} => {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Suspend failed for {packageFullName}");
            return false;
        }
    }

    public async Task<bool> TerminatePackageAsync(string packageFullName)
    {
        if (!_configured) return false;
        try
        {
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(packageFullName));
            var encoded = Uri.EscapeDataString(b64);
            var url = $"/api/taskmanager/app?package={encoded}";
            Logger.Info($"Terminate: {packageFullName}");
            var response = await DeleteWithCsrfAsync(url);
            Logger.Info($"DELETE {url} => {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Terminate failed for {packageFullName}");
            return false;
        }
    }

    private static string? TryParseError(string? body)
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
                    await Task.Delay(PollDelayMs);
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
            catch (Exception ex)
            {
                Logger.Trace($"Package manager polling error (ignored): {ex.Message}");
            }
            await Task.Delay(RetryDelayMs);
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

    public async Task<byte[]?> CaptureScreenshotAsync(CancellationToken ct = default)
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
            var response = await _http.GetAsync(url, ct);
            Logger.Info($"GET screenshot => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Logger.Info("CaptureScreenshot cancelled");
            return null;
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

    public async Task<string?> GetCrashDumpsAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetCrashDumps called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/app/debug/crashdump");
            var response = await _http.GetAsync("/api/app/debug/crashdump");
            Logger.Info($"GET /api/app/debug/crashdump => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/app/debug/crashdump failed: {await ReadResponseBody(response)}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetCrashDumps failed");
            return null;
        }
    }

    public async Task<bool> DeleteCrashDumpAsync(string filename)
    {
        if (!_configured)
        {
            Logger.Warn("DeleteCrashDump called but not configured");
            return false;
        }

        try
        {
            var encoded = Uri.EscapeDataString(filename);
            Logger.Info($"DELETE /api/app/debug/crashdump/{encoded}");
            var response = await DeleteWithCsrfAsync($"/api/app/debug/crashdump/{encoded}");
            Logger.Info($"DELETE crashdump => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"DeleteCrashDump failed for {filename}");
            return false;
        }
    }

    public async Task<string?> GetCrashControlAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetCrashControl called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/app/debug/crashcontrol");
            var response = await _http.GetAsync("/api/app/debug/crashcontrol");
            Logger.Info($"GET /api/app/debug/crashcontrol => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/app/debug/crashcontrol failed: {await ReadResponseBody(response)}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetCrashControl failed");
            return null;
        }
    }

    public async Task<bool> SetCrashControlAsync(bool enabled)
    {
        if (!_configured)
        {
            Logger.Warn("SetCrashControl called but not configured");
            return false;
        }

        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("CrashDumpEnabled", enabled ? "true" : "false")
            });
            Logger.Info($"POST /api/app/debug/crashcontrol (enabled={enabled})");
            var response = await PostWithCsrfAsync("/api/app/debug/crashcontrol", content);
            Logger.Info($"POST /api/app/debug/crashcontrol => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "SetCrashControl failed");
            return false;
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

    public async Task<string?> GetNetworkConfigAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetNetworkConfig called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/networking/ipconfig");
            var response = await _http.GetAsync("/api/networking/ipconfig");
            Logger.Info($"GET /api/networking/ipconfig => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetNetworkConfig failed");
            return null;
        }
    }

    public async Task<string?> GetWifiInterfacesAsync()
    {
        if (!_configured)
        {
            Logger.Warn("GetWifiInterfaces called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/wifi/interfaces");
            var response = await _http.GetAsync("/api/wifi/interfaces");
            Logger.Info($"GET /api/wifi/interfaces => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetWifiInterfaces failed");
            return null;
        }
    }

    public async Task<string?> GetWifiNetworksAsync(string interfaceGuid)
    {
        if (!_configured)
        {
            Logger.Warn("GetWifiNetworks called but not configured");
            return null;
        }

        try
        {
            var path = $"/api/wifi/networks?interface={interfaceGuid}";
            Logger.Info($"GET {path}");
            var response = await _http.GetAsync(path);
            Logger.Info($"GET {path} => {(int)response.StatusCode}");
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
                catch (Exception ex) { Logger.Trace($"WS close error (ignored): {ex.Message}"); }
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

public record SshConnectionInfo(string Host, int Port, string Username, string Password);

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
