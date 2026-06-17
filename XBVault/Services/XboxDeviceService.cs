using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public class XboxDeviceService
{
    private HttpClient _http;
    private bool _configured;

    public XboxDeviceService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _http = new HttpClient(handler);
    }

    public void Configure(string baseUrl, string username, string password)
    {
        var maskedPw = password.Length > 0 ? $"{password[0]}***" : "";
        Logger.Debug($"XboxDeviceService.Configure({baseUrl}, {username}, {maskedPw})");

        var auth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));

        // Fresh client each call — BaseAddress freezes after first request
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
        http.BaseAddress = new Uri(baseUrl);

        var old = _http;
        _http = http;
        old.Dispose();
        _configured = true;
        Logger.Debug("XboxDeviceService configured");
    }

    public bool IsConfigured => _configured;

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        if (!_configured)
        {
            Logger.Warn("TestConnection called but not configured");
            return new ConnectionTestResult(false, null, null);
        }

        try
        {
            Logger.Debug("GET /api/os/info");
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var response = await _http.GetAsync("/api/os/info", linkedCts.Token);
            Logger.Debug($"Connection test response: {(int)response.StatusCode} {response.ReasonPhrase}");
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
            Logger.Debug("GET /api/app/packagemanager/packages");
            var response = await _http.GetAsync("/api/app/packagemanager/packages");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Logger.Trace($"Packages JSON length: {json.Length} chars");
            var result = JsonSerializer.Deserialize<PackagesResponse>(json);

            var count = result?.InstalledPackages?.Count ?? 0;
            Logger.Debug($"Got {count} installed packages");
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
            var response = await _http.DeleteAsync(
                $"/api/app/packagemanager/package?package={encoded}");
            Logger.Debug($"Uninstall response: {(int)response.StatusCode}");
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
        if (!_configured)
        {
            Logger.Warn("Install called but not configured");
            return false;
        }
        if (!File.Exists(filePath))
        {
            Logger.Error($"Install file not found: {filePath}");
            return false;
        }

        try
        {
            progress?.Report(0);

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(filePath);
            Logger.Info($"Installing: {fileName} ({fileBytes.Length} bytes)");

            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileBytes), "package", fileName);

            progress?.Report(0.5);

            Logger.Debug($"POST /api/app/packagemanager/package with {fileName}");
            var response = await _http.PostAsync("/api/app/packagemanager/package", content);

            progress?.Report(1.0);
            Logger.Debug($"Install response: {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Install failed for {filePath}");
            return false;
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
