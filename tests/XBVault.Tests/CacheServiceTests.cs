namespace XBVault.Tests;

public class CacheServiceTests : IDisposable
{
    private readonly string _cacheRoot;
    private readonly CacheService _cache;

    public CacheServiceTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        _cache = new CacheService(_cacheRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
            Directory.Delete(_cacheRoot, true);
    }

    [Fact]
    public void GetAppCacheDir_CreatesDirectory()
    {
        var dir = _cache.GetAppCacheDir("test-app");

        Assert.True(Directory.Exists(dir));
        Assert.EndsWith(Path.Combine("test-app"), dir);
    }

    [Fact]
    public void GetDownloadPath_IsUnderAppCacheDir()
    {
        var path = _cache.GetDownloadPath("app-1", "file.appx");

        Assert.EndsWith(Path.Combine("app-1", "file.appx"), path);
        Assert.StartsWith(_cacheRoot, path);
    }

    [Fact]
    public void IsCached_FalseWhenMissing()
    {
        Assert.False(_cache.IsCached("app-1", "missing.appx"));
    }

    [Fact]
    public void IsCached_TrueWhenFileExists()
    {
        var path = _cache.GetDownloadPath("app-1", "present.appx");
        File.WriteAllText(path, "data");

        Assert.True(_cache.IsCached("app-1", "present.appx"));
    }

    [Fact]
    public void GetCacheSizeBytes_ZeroWhenEmpty()
    {
        Assert.Equal(0, _cache.GetCacheSizeBytes());
    }

    [Fact]
    public void GetCacheSizeBytes_CountsNestedFiles()
    {
        var path = _cache.GetDownloadPath("app-1", "file.bin");
        var subPath = Path.Combine(Path.GetDirectoryName(path)!, "sub", "file.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(subPath)!);
        File.WriteAllText(subPath, new string('x', 1000));

        Assert.Equal(1000, _cache.GetCacheSizeBytes());
    }

    [Fact]
    public void ClearAppCache_RemovesOnlyThatApp()
    {
        _cache.GetDownloadPath("app-keep", "a.appx");
        _cache.GetDownloadPath("app-del", "b.appx");

        _cache.ClearAppCache("app-del");

        Assert.True(Directory.Exists(_cache.GetAppCacheDir("app-keep")));
        Assert.False(Directory.Exists(Path.Combine(_cacheRoot, "app-del")));
    }

    [Fact]
    public void ClearAppCache_NoThrowWhenMissing()
    {
        _cache.ClearAppCache("never-created");
    }

    [Fact]
    public void ClearCache_EmptiesEverything()
    {
        _cache.GetDownloadPath("app-1", "a.appx");
        Assert.True(Directory.Exists(_cacheRoot));

        _cache.ClearCache();

        Assert.True(Directory.Exists(_cacheRoot));
        Assert.Equal(0, _cache.GetCacheSizeBytes());
    }

    [Fact]
    public void GetThumbnailPath_StableHashAndPng()
    {
        var p1 = _cache.GetThumbnailPath("https://example.com/img.png");
        var p2 = _cache.GetThumbnailPath("https://example.com/img.png");

        Assert.Equal(p1, p2);
        Assert.EndsWith(".png", p1);
        Assert.StartsWith(Path.Combine(_cacheRoot, "thumbnails"), p1);
    }

    [Fact]
    public void GetThumbnailPath_DifferentUrlsDiffer()
    {
        var p1 = _cache.GetThumbnailPath("https://example.com/a.png");
        var p2 = _cache.GetThumbnailPath("https://example.com/b.png");

        Assert.NotEqual(p1, p2);
    }

    [Fact]
    public async Task SaveAndLoadThumbnail_RoundTrip()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var url = "https://example.com/thumb.png";

        await _cache.SaveThumbnailAsync(url, data);
        var loaded = await _cache.TryLoadThumbnailDataAsync(url);

        Assert.NotNull(loaded);
        Assert.Equal(data, loaded);
    }

    [Fact]
    public async Task TryLoadThumbnail_ReturnsNullWhenMissing()
    {
        var loaded = await _cache.TryLoadThumbnailDataAsync("https://example.com/nope.png");

        Assert.Null(loaded);
    }
}
