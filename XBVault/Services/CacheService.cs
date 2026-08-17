#nullable enable
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
    private readonly IAppLogger _log;

    // Back-compat ctor for tests and direct instantiation
    public CacheService(string? cacheRoot = null)
        : this(ServiceLocator.Resolve<IAppLogger>(), cacheRoot)
    {
    }

    public CacheService(IAppLogger log, string? cacheRoot = null)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _cacheRoot = cacheRoot ?? DefaultCacheRoot;
    }

    public string GetAppCacheDir(string appId)
    {
        var dir = Path.Combine(_cacheRoot, appId);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            _log.Trace($"Created cache dir for appId={appId}: {dir}");
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
        _log.Trace($"Cache check: appId={appId} file={fileName} → {cached}");
        return cached;
    }

    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            _log.Trace("Cache root does not exist, size=0");
            return 0;
        }

        var size = Directory.GetFiles(_cacheRoot, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
        _log.Debug($"Cache total size: {size} bytes");
        return size;
    }

    public void ClearCache()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            _log.Debug("Cache root does not exist, nothing to clear");
            return;
        }
        var before = GetCacheSizeBytes();
        Directory.Delete(_cacheRoot, true);
        Directory.CreateDirectory(_cacheRoot);
        _log.Info($"Cache cleared (was {before} bytes)");
    }

    public void ClearAppCache(string appId)
    {
        var dir = GetAppCacheDir(appId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
            _log.Debug($"Cache cleared for appId={appId}");
        }
        else
        {
            _log.Trace($"No cache to clear for appId={appId}");
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
        _log.Trace($"Thumbnail cached: {path}");
    }

    public async Task<byte[]?> TryLoadThumbnailDataAsync(string url)
    {
        var path = GetThumbnailPath(url);
        if (!File.Exists(path))
            return null;
        _log.Trace($"Thumbnail cache hit: {path}");
        return await File.ReadAllBytesAsync(path);
    }
}
