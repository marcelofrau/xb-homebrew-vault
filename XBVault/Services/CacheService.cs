using System.IO;
using System.Text.Json;

namespace XBVault.Services;

public class CacheService
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XBVault", "cache");

    public string GetAppCacheDir(string appId)
    {
        var dir = Path.Combine(CacheRoot, appId);
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
        if (!Directory.Exists(CacheRoot))
        {
            Logger.Trace("Cache root does not exist, size=0");
            return 0;
        }

        var size = Directory.GetFiles(CacheRoot, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
        Logger.Debug($"Cache total size: {size} bytes");
        return size;
    }

    public void ClearCache()
    {
        if (!Directory.Exists(CacheRoot))
        {
            Logger.Debug("Cache root does not exist, nothing to clear");
            return;
        }
        var before = GetCacheSizeBytes();
        Directory.Delete(CacheRoot, true);
        Directory.CreateDirectory(CacheRoot);
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
}
