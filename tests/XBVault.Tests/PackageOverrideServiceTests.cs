using System.Net;

namespace XBVault.Tests;

public class PackageOverrideServiceTests
{
    private const string Json = """
        {
          "PackageFamilyNameOverrides": [
            { "PackageFamilyName": "Foo_8wekyb3d8bbwe", "CatalogId": "catalog-foo", "ImageUrl": "https://img.example/foo.png" }
          ],
          "PackageNameOverrides": [
            { "PackageName": "Bar Game", "CatalogId": "catalog-bar" }
          ]
        }
        """;

    [Fact]
    public void ParseAndMerge_PopulatesPfnLookup()
    {
        using var service = new PackageOverrideService();
        service.ParseAndMerge(Json);

        Assert.True(service.TryGetCatalogId("Foo_8wekyb3d8bbwe", out var id));
        Assert.Equal("catalog-foo", id);
        Assert.True(service.TryGetImageUrl("Foo_8wekyb3d8bbwe", out var img));
        Assert.Equal("https://img.example/foo.png", img);
    }

    [Fact]
    public void ParseAndMerge_PopulatesNameLookup()
    {
        using var service = new PackageOverrideService();
        service.ParseAndMerge(Json);

        Assert.True(service.TryGetCatalogIdByName("Bar Game", out var id));
        Assert.Equal("catalog-bar", id);
        Assert.False(service.TryGetImageUrlByName("Bar Game", out _));
    }

    [Fact]
    public void ParseAndMerge_UnknownKey_ReturnsFalse()
    {
        using var service = new PackageOverrideService();
        service.ParseAndMerge(Json);

        Assert.False(service.TryGetCatalogId("Unknown_123", out _));
        Assert.False(service.TryGetCatalogIdByName("Nope", out _));
    }

    [Fact]
    public void ParseAndMerge_IsCaseInsensitive()
    {
        using var service = new PackageOverrideService();
        service.ParseAndMerge(Json);

        Assert.True(service.TryGetCatalogId("foo_8WEKYB3D8BBWE", out _));
        Assert.True(service.TryGetCatalogIdByName("bar game", out _));
    }

    [Fact]
    public void ParseAndMerge_InvalidJson_NoThrow()
    {
        using var service = new PackageOverrideService();
        service.ParseAndMerge("{ not valid json");

        Assert.False(service.TryGetCatalogId("Foo_8wekyb3d8bbwe", out _));
    }

    [Fact]
    public void ParseAndMerge_WhitespaceKeys_Ignored()
    {
        using var service = new PackageOverrideService();
        service.ParseAndMerge("""
            { "PackageFamilyNameOverrides": [ { "PackageFamilyName": "   ", "CatalogId": "x" } ] }
            """);

        Assert.False(service.TryGetCatalogId("", out _));
    }

    [Fact]
    public async Task FetchRemoteAsync_MergesRemoteOverrides()
    {
        var handler = StubHttpMessageHandler.Json(Json);
        using var http = new HttpClient(handler);
        using var service = new PackageOverrideService(http);

        await service.FetchRemoteAsync();

        Assert.True(service.TryGetCatalogId("Foo_8wekyb3d8bbwe", out var id));
        Assert.Equal("catalog-foo", id);
    }

    [Fact]
    public async Task FetchRemoteAsync_OnHttpError_KeepsEmbeddedOnly()
    {
        var handler = StubHttpMessageHandler.Json("", HttpStatusCode.NotFound);
        using var http = new HttpClient(handler);
        using var service = new PackageOverrideService(http);
        service.ParseAndMerge(Json);

        await service.FetchRemoteAsync();

        Assert.True(service.TryGetCatalogId("Foo_8wekyb3d8bbwe", out _));
    }

    [Fact]
    public async Task FetchRemoteAsync_EmptyBody_KeepsEmbeddedOnly()
    {
        var handler = StubHttpMessageHandler.Json("");
        using var http = new HttpClient(handler);
        using var service = new PackageOverrideService(http);
        service.ParseAndMerge(Json);

        await service.FetchRemoteAsync();

        Assert.True(service.TryGetCatalogId("Foo_8wekyb3d8bbwe", out _));
    }

    [Fact]
    public async Task FetchRemoteAsync_SendsUserAgent()
    {
        var handler = StubHttpMessageHandler.Json(Json);
        using var http = new HttpClient(handler);
        using var service = new PackageOverrideService(http);

        await service.FetchRemoteAsync();

        Assert.NotEmpty(handler.Requests);
        Assert.Contains("XB Homebrew Vault", handler.Requests[0].Headers.UserAgent.ToString());
    }
}
