using System.Text.Json;
using XBVault.Services;

namespace XBVault.Tests;

public class LocalOverrideServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public LocalOverrideServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "local-overrides.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void AddOrUpdate_ThenTryGet_ResolvesId()
    {
        var svc = new LocalOverrideService(_path);
        svc.AddOrUpdate("realname", "game");

        Assert.True(svc.TryGetCatalogIdByName("realname", out var id));
        Assert.Equal("game", id);
    }

    [Fact]
    public void TryGet_NoFile_ReturnsFalse()
    {
        var svc = new LocalOverrideService(_path);
        Assert.False(svc.TryGetCatalogIdByName("realname", out _));
        Assert.Equal(0, svc.Count);
    }

    [Fact]
    public void TryGet_EmptyOrNullName_ReturnsFalse()
    {
        var svc = new LocalOverrideService(_path);
        Assert.False(svc.TryGetCatalogIdByName(" ", out _));
        Assert.False(svc.TryGetCatalogIdByName(null!, out _));
    }

    [Fact]
    public void Lookup_IsCaseInsensitive()
    {
        var svc = new LocalOverrideService(_path);
        svc.AddOrUpdate("REALNAME", "game");

        Assert.True(svc.TryGetCatalogIdByName("realname", out var id));
        Assert.Equal("game", id);
    }

    [Fact]
    public void AddOrUpdate_PersistsToDisk_AndReloads()
    {
        var svc = new LocalOverrideService(_path);
        svc.AddOrUpdate("shipwright", "soh");
        Assert.True(File.Exists(_path));

        var reloaded = new LocalOverrideService(_path);
        reloaded.Load();
        Assert.True(reloaded.TryGetCatalogIdByName("shipwright", out var id));
        Assert.Equal("soh", id);
    }

    [Fact]
    public void AddOrUpdate_OverwritesExistingValue()
    {
        var svc = new LocalOverrideService(_path);
        svc.AddOrUpdate("realname", "game");
        svc.AddOrUpdate("realname", "other");

        Assert.True(svc.TryGetCatalogIdByName("realname", out var id));
        Assert.Equal("other", id);
    }

    [Fact]
    public void Remove_DeletesEntry_AndPersists()
    {
        var svc = new LocalOverrideService(_path);
        svc.AddOrUpdate("realname", "game");

        Assert.True(svc.Remove("realname"));
        Assert.False(svc.TryGetCatalogIdByName("realname", out _));
        Assert.Equal(0, svc.Count);
    }

    [Fact]
    public void Remove_MissingEntry_ReturnsFalse()
    {
        var svc = new LocalOverrideService(_path);
        Assert.False(svc.Remove("nope"));
    }

    [Fact]
    public void Load_CorruptJson_IgnoresAndStartsEmpty()
    {
        File.WriteAllText(_path, "{ not valid json ");
        var svc = new LocalOverrideService(_path);
        svc.Load();
        Assert.Equal(0, svc.Count);
    }

    [Fact]
    public void Load_SkipsBlankEntries()
    {
        var json = JsonSerializer.Serialize(new
        {
            Overrides = new[]
            {
                new { PackageName = "good", CatalogId = "soh" },
                new { PackageName = "  ", CatalogId = "x" },
                new { PackageName = "y", CatalogId = "" }
            }
        });
        File.WriteAllText(_path, json);

        var svc = new LocalOverrideService(_path);
        svc.Load();
        Assert.Equal(1, svc.Count);
        Assert.True(svc.TryGetCatalogIdByName("good", out var id));
        Assert.Equal("soh", id);
    }

    [Fact]
    public void Entries_OrderedByPackageName()
    {
        var svc = new LocalOverrideService(_path);
        svc.AddOrUpdate("zeta", "1");
        svc.AddOrUpdate("alpha", "2");

        var names = svc.Entries.Select(e => e.PackageName).ToArray();
        Assert.Equal(new[] { "alpha", "zeta" }, names);
    }
}
