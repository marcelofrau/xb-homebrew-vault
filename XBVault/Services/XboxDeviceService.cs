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

    public async Task<bool> TestConnectionAsync()
    {
        if (!_configured)
        {
            Logger.Warn("TestConnection called but not configured");
            return false;
        }

        try
        {
            Logger.Debug("GET /api/os/info");
            var response = await _http.GetAsync("/api/os/info");
            Logger.Debug($"Connection test response: {(int)response.StatusCode} {response.ReasonPhrase}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Connection test failed");
            return false;
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
