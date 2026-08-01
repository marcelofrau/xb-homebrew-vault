using XBVault.Models;

namespace XBVault.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var settings = new AppSettings
        {
            LastSelectedTab = "Inspector",
            CacheExpiryHours = 12,
            CheckForUpdatesOnStartup = false,
            MinLogLevel = "Debug",
            LogFontSize = 18,
            XboxConnection = new XboxConnection
            {
                Address = "192.168.0.10",
                Username = "DevModeUser",
                EncryptedPassword = "obfuscated-value",
                UseHttps = false,
                Port = 8080
            }
        };

        SettingsService.SaveTo(_path, settings);
        var loaded = SettingsService.LoadFrom(_path);

        Assert.Equal("Inspector", loaded.LastSelectedTab);
        Assert.Equal(12, loaded.CacheExpiryHours);
        Assert.False(loaded.CheckForUpdatesOnStartup);
        Assert.Equal("Debug", loaded.MinLogLevel);
        Assert.Equal(18, loaded.LogFontSize);
        Assert.Equal("192.168.0.10", loaded.XboxConnection.Address);
        Assert.Equal("DevModeUser", loaded.XboxConnection.Username);
        Assert.Equal("obfuscated-value", loaded.XboxConnection.EncryptedPassword);
        Assert.False(loaded.XboxConnection.UseHttps);
        Assert.Equal(8080, loaded.XboxConnection.Port);
    }

    [Fact]
    public void SaveTo_CreatesDirectory()
    {
        var nested = Path.Combine(_dir, "a", "b", "settings.json");

        SettingsService.SaveTo(nested, new AppSettings());

        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void LoadFrom_MissingFile_ReturnsDefaults()
    {
        var loaded = SettingsService.LoadFrom(_path);

        Assert.Equal("Browse", loaded.LastSelectedTab);
        Assert.Equal(24, loaded.CacheExpiryHours);
        Assert.True(loaded.CheckForUpdatesOnStartup);
        Assert.True(loaded.XboxConnection.UseHttps);
        Assert.Equal(11443, loaded.XboxConnection.Port);
    }

    [Fact]
    public void LoadFrom_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ invalid json !!!");

        var loaded = SettingsService.LoadFrom(_path);

        Assert.Equal("Browse", loaded.LastSelectedTab);
        Assert.Equal(24, loaded.CacheExpiryHours);
    }

    [Fact]
    public void LoadFrom_PartialFile_UsesDefaultsForMissingFields()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, """{"LastSelectedTab":"Logs"}""");

        var loaded = SettingsService.LoadFrom(_path);

        Assert.Equal("Logs", loaded.LastSelectedTab);
        Assert.Equal(24, loaded.CacheExpiryHours);
    }

    [Fact]
    public void SaveThenLoad_IgnoresUnknownJsonFields()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, """{"UnknownFutureField":"x","LastSelectedTab":"Browse"}""");

        var loaded = SettingsService.LoadFrom(_path);

        Assert.Equal("Browse", loaded.LastSelectedTab);
    }
}
