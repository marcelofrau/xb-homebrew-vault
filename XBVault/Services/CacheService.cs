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
            Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetDownloadPath(string appId, string fileName)
    {
        return Path.Combine(GetAppCacheDir(appId), fileName);
    }

    public bool IsCached(string appId, string fileName)
    {
        var path = GetDownloadPath(appId, fileName);
        return File.Exists(path);
    }

    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(CacheRoot)) return 0;

        return Directory.GetFiles(CacheRoot, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
    }

    public void ClearCache()
    {
        if (!Directory.Exists(CacheRoot)) return;
        Directory.Delete(CacheRoot, true);
        Directory.CreateDirectory(CacheRoot);
    }

    public void ClearAppCache(string appId)
    {
        var dir = GetAppCacheDir(appId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }
}
