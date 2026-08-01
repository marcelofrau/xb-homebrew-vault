using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace XBVault.Services;

public class CacheService
{
    private static readonly string DefaultCacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XBVault", "cache");

    private readonly string _cacheRoot;

    public CacheService(string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot ?? DefaultCacheRoot;
    }

    public string GetAppCacheDir(string appId)
    {
        var dir = Path.Combine(_cacheRoot, appId);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Logger.Trace($"Created cache dir for appId={appId}: {dir}");
        }
        return dir;
    }

    public string GetDownloadPath(string appId, string fileName)
    {
        return Path.Combine(GetAppCacheDir(appId), fileName);
    }

    public bool IsCached(string appId, string fileName)
    {
        var path = GetDownloadPath(appId, fileName);
        var cached = File.Exists(path);
        Logger.Trace($"Cache check: appId={appId} file={fileName} → {cached}");
        return cached;
    }

    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            Logger.Trace("Cache root does not exist, size=0");
            return 0;
        }

        var size = Directory.GetFiles(_cacheRoot, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
        Logger.Debug($"Cache total size: {size} bytes");
        return size;
    }

    public void ClearCache()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            Logger.Debug("Cache root does not exist, nothing to clear");
            return;
        }
        var before = GetCacheSizeBytes();
        Directory.Delete(_cacheRoot, true);
        Directory.CreateDirectory(_cacheRoot);
        Logger.Info($"Cache cleared (was {before} bytes)");
    }

    public void ClearAppCache(string appId)
    {
        var dir = GetAppCacheDir(appId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
            Logger.Debug($"Cache cleared for appId={appId}");
        }
        else
        {
            Logger.Trace($"No cache to clear for appId={appId}");
        }
    }

    public string GetThumbnailCacheDir()
    {
        var dir = Path.Combine(_cacheRoot, "thumbnails");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetThumbnailPath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        return Path.Combine(GetThumbnailCacheDir(), $"{hash}.png");
    }

    public async Task SaveThumbnailAsync(string url, byte[] data)
    {
        var path = GetThumbnailPath(url);
        await File.WriteAllBytesAsync(path, data);
        Logger.Trace($"Thumbnail cached: {path}");
    }

    public async Task<byte[]?> TryLoadThumbnailDataAsync(string url)
    {
        var path = GetThumbnailPath(url);
        if (!File.Exists(path))
            return null;
        Logger.Trace($"Thumbnail cache hit: {path}");
        return await File.ReadAllBytesAsync(path);
    }
}
