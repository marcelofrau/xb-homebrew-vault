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
        {
            Logger.Error($"No download URL for {item.Name}");
            return false;
        }

        progress?.Report(0);
        Logger.Info($"DownloadAndInstall: {item.Name} from {item.DownloadUrl}");

        var fileName = GetFileNameFromUrl(item.DownloadUrl);
        var localPath = _cache.GetDownloadPath(item.Id, fileName);
        Logger.Debug($"Target local path: {localPath}");

        // Download if not cached
        if (_cache.IsCached(item.Id, fileName))
        {
            Logger.Debug($"Cache hit for {item.Id}/{fileName}");
        }
        else
        {
            Logger.Debug($"Cache miss — downloading {fileName}");
            progress?.Report(0.1);

            try
            {
                var response = await _http.GetAsync(item.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1;
                Logger.Debug($"Download size: {(total > 0 ? $"{total} bytes" : "unknown")}");
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

                Logger.Info($"Downloaded {read} bytes to {localPath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Download failed for {item.DownloadUrl}");
                if (File.Exists(localPath))
                    File.Delete(localPath);
                return false;
            }
        }

        progress?.Report(0.5);

        // Analyze package for dependencies
        Logger.Trace("Analyzing package dependencies (stub)");
        var dependencies = AnalyzePackage(localPath);
        Logger.Debug($"Dependencies found: {dependencies.Length}");
        progress?.Report(0.6);

        // Install via Xbox
        Logger.Debug("Sending package to Xbox for install");
        var result = await _xbox.InstallPackageAsync(localPath,
            new Progress<double>(p => progress?.Report(0.6 + (0.4 * p))));

        progress?.Report(1.0);
        Logger.Info($"Install result for {item.Name}: {(result ? "success" : "failed")}");
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
