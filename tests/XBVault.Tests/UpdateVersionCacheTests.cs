namespace XBVault.Tests;

public class UpdateVersionCacheTests : IDisposable
{
    private readonly string _dir;
    private readonly string _filePath;

    public UpdateVersionCacheTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_dir, "update-versions.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void NewCache_Empty_NotSuppressed()
    {
        var cache = new UpdateVersionCache(_filePath);

        Assert.False(cache.TryGetSuppressed("game", "1.0.0", "0.9.0"));
    }

    [Fact]
    public void RecordUpdate_ThenSuppressed()
    {
        var cache = new UpdateVersionCache(_filePath);
        cache.RecordUpdate("game", "1.0.0", "1.0.0");

        Assert.True(cache.TryGetSuppressed("game", "1.0.0", "1.0.0"));
    }

    [Fact]
    public void RecordUpdate_NotSuppressedWhenVersionsDiffer()
    {
        var cache = new UpdateVersionCache(_filePath);
        cache.RecordUpdate("game", "1.0.0", "1.0.0");

        Assert.False(cache.TryGetSuppressed("game", "1.1.0", "1.0.0"));
        Assert.False(cache.TryGetSuppressed("game", "1.0.0", "0.5.0"));
        Assert.False(cache.TryGetSuppressed("other-game", "1.0.0", "1.0.0"));
    }

    [Fact]
    public void RecordUpdate_OutdatedPair_NotSuppressed()
    {
        var cache = new UpdateVersionCache(_filePath);

        // stale entry from an old scan: catalog ahead of installed
        cache.RecordUpdate("game", "1.2.0", "1.0.0");

        Assert.False(cache.TryGetSuppressed("game", "1.2.0", "1.0.0"));
    }

    [Fact]
    public void RecordUpdate_UnparseableVersions_NotSuppressed()
    {
        var cache = new UpdateVersionCache(_filePath);
        cache.RecordUpdate("game", "abc", "xyz");

        Assert.False(cache.TryGetSuppressed("game", "abc", "xyz"));
    }

    [Fact]
    public void RecordUpdate_FileCreated()
    {
        var cache = new UpdateVersionCache(_filePath);
        cache.RecordUpdate("game", "1.0.0", "1.0.0");

        Assert.True(File.Exists(_filePath));
        Assert.Contains("game", File.ReadAllText(_filePath));
    }

    [Fact]
    public void Reload_PersistsAcrossInstances()
    {
        var first = new UpdateVersionCache(_filePath);
        first.RecordUpdate("game", "1.0.0", "1.0.0");

        var second = new UpdateVersionCache(_filePath);

        Assert.True(second.TryGetSuppressed("game", "1.0.0", "1.0.0"));
    }

    [Fact]
    public void CorruptFile_LoadsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_filePath, "{ invalid json !!!");

        var cache = new UpdateVersionCache(_filePath);

        Assert.False(cache.TryGetSuppressed("game", "1.0.0", "1.0.0"));
    }

    [Fact]
    public void MissingFile_LoadsEmpty_NoThrow()
    {
        var cache = new UpdateVersionCache(_filePath);

        Assert.False(cache.TryGetSuppressed("game", "1.0.0", "1.0.0"));
    }

    [Fact]
    public void MultipleEntries_Independent()
    {
        var cache = new UpdateVersionCache(_filePath);
        cache.RecordUpdate("game-a", "1.0.0", "1.0.0");
        cache.RecordUpdate("game-b", "2.0.0", "2.0.0");

        Assert.True(cache.TryGetSuppressed("game-a", "1.0.0", "1.0.0"));
        Assert.True(cache.TryGetSuppressed("game-b", "2.0.0", "2.0.0"));
        Assert.False(cache.TryGetSuppressed("game-a", "2.0.0", "2.0.0"));
    }
}
