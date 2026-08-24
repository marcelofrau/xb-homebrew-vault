using XBVault.Services;

namespace XBVault.Tests;

public class UrlResolverServiceTests
{
    [Theory]
    [InlineData("https://example.com/game.appx", true)]
    [InlineData("https://example.com/game.appxbundle", true)]
    [InlineData("https://example.com/game.msix", true)]
    [InlineData("https://example.com/game.msixbundle", true)]
    [InlineData("https://example.com/game.zip", true)]
    [InlineData("https://example.com/game.xvc", true)]
    [InlineData("https://example.com/game.eappx", true)]
    [InlineData("https://example.com/game.html", false)]
    [InlineData("https://example.com/game.txt", false)]
    [InlineData("https://gofile.io/d/AbCdEf", false)]
    [InlineData("https://drive.google.com/file/d/1abc/view", false)]
    public void IsDirectLink_KnownExtensions_ReturnsCorrectly(string url, bool expected)
    {
        Assert.Equal(expected, UrlResolverService.IsDirectLink(url));
    }

    [Fact]
    public async Task ResolveAsync_DirectLink_ReturnsUnchanged()
    {
        var url = "https://example.com/game.appx";
        var (resolved, fileName) = await UrlResolverService.ResolveAsync(url);
        Assert.Equal(url, resolved);
        Assert.Null(fileName);
    }

    [Theory]
    [InlineData("https://gofile.io/d/AbCdEf")]
    [InlineData("https://gofile.io/download/AbCdEf")]
    [InlineData("https://www.gofile.io/d/XyZ123")]
    [InlineData("https://drive.google.com/file/d/1abcDeFgHiJ/view?usp=sharing")]
    [InlineData("https://drive.google.com/open?id=1abcDeFgHiJ")]
    [InlineData("https://1drv.ms/u/s!abc123")]
    [InlineData("https://onedrive.live.com/redir?resid=123")]
    [InlineData("https://example.sharepoint.com/:w:/s/drive/Efg123")]
    public async Task ResolveAsync_KnownHosts_DoesNotThrow(string url)
    {
        // These will fail at the HTTP level (no network in tests) but should not throw
        // pattern-matching or parsing errors
        try
        {
            await UrlResolverService.ResolveAsync(url);
        }
        catch (HttpRequestException)
        {
            // Expected — no network access in tests
        }
        catch (InvalidOperationException)
        {
            // Expected — API calls will fail but pattern matching should succeed
        }
    }

    [Fact]
    public async Task ResolveAsync_UnknownHost_FallsBackToOriginal()
    {
        // An unknown host should fall through to generic resolver and return the original URL
        try
        {
            var url = "https://unknown-host.example.com/file";
            var (resolved, _) = await UrlResolverService.ResolveAsync(url);
            Assert.Equal(url, resolved);
        }
        catch (HttpRequestException)
        {
            // Expected — no network access in tests
        }
    }
}
