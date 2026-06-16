using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using XBVault.Helpers;
using XBVault.Models;

namespace XBVault.Services;

public class PackageInstallService
{
    private readonly HttpClient _http;
    private readonly CacheService _cache;
    private readonly XboxDeviceService _xbox;

    public PackageInstallService(CacheService cache, XboxDeviceService xbox)
    {
        _cache = cache;
        _xbox = xbox;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", $"XB Homebrew Vault/{BuildInfo.Version}");
    }

    public async Task<bool> DownloadAndInstallAsync(
        CatalogItem item,
        IProgress<double>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(item.DownloadUrl))
            return false;

        progress?.Report(0);

        var fileName = GetFileNameFromUrl(item.DownloadUrl);
        var localPath = _cache.GetDownloadPath(item.Id, fileName);

        // Download if not cached
        if (!_cache.IsCached(item.Id, fileName))
        {
            progress?.Report(0.1);

            try
            {
                var response = await _http.GetAsync(item.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1;
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(localPath);

                var buffer = new byte[81920];
                long read = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    read += bytesRead;

                    if (total > 0)
                    {
                        progress?.Report(0.1 + (0.4 * (double)read / total));
                    }
                }
            }
            catch
            {
                if (File.Exists(localPath))
                    File.Delete(localPath);
                return false;
            }
        }

        progress?.Report(0.5);

        // Analyze package for dependencies
        var dependencies = AnalyzePackage(localPath);
        progress?.Report(0.6);

        // Install via Xbox
        var result = await _xbox.InstallPackageAsync(localPath,
            new Progress<double>(p => progress?.Report(0.6 + (0.4 * p))));

        progress?.Report(1.0);
        return result;
    }

    private static string GetFileNameFromUrl(string url)
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(fileName) ? "package.appx" : fileName;
    }

    private static string[] AnalyzePackage(string filePath)
    {
        // Phase 3: stub — real analysis requires parsing MSIX/APPX
        // For now, return empty dependency list
        return [];
    }
}
