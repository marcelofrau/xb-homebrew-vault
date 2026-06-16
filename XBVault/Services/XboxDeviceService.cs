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
    private readonly HttpClient _http;
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
        var auth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        _http.BaseAddress = new Uri(baseUrl);
        _configured = true;
    }

    public bool IsConfigured => _configured;

    public async Task<bool> TestConnectionAsync()
    {
        if (!_configured) return false;

        try
        {
            var response = await _http.GetAsync("/api/os/info");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<InstalledPackage>> GetInstalledPackagesAsync()
    {
        if (!_configured) return [];

        try
        {
            var response = await _http.GetAsync("/api/app/packagemanager/packages");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PackagesResponse>(json);

            return result?.InstalledPackages ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> UninstallPackageAsync(string packageFullName)
    {
        if (!_configured) return false;

        try
        {
            var encoded = Uri.EscapeDataString(packageFullName);
            var response = await _http.DeleteAsync(
                $"/api/app/packagemanager/package?package={encoded}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null)
    {
        if (!_configured || !File.Exists(filePath)) return false;

        try
        {
            progress?.Report(0);

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(filePath);

            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileBytes), "package", fileName);

            progress?.Report(0.5);

            var response = await _http.PostAsync("/api/app/packagemanager/package", content);

            progress?.Report(1.0);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

internal class PackagesResponse
{
    public List<InstalledPackage> InstalledPackages { get; set; } = [];
}
