using System.Net;

namespace XBVault.Tests;

public class GitHubReleaseCheckerServiceTests
{
    [Theory]
    [InlineData(null, "1.0.0", false)]
    [InlineData("", "1.0.0", false)]
    [InlineData("v1.0.0", "0.9.0", true)]
    [InlineData("1.0.0", "0.9.0", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("v1.0.0", "1.1.0", false)]
    [InlineData("v1.0.0+abcdef", "0.9.0", true)]
    [InlineData("v1.0.0-beta.1", "0.9.0", true)]
    [InlineData("not-a-version", "1.0.0", false)]
    [InlineData("v0.0.0", "0.0.0", false)]
    public void IsNewerVersion_Compares(string? tag, string current, bool expected)
    {
        Assert.Equal(expected, GitHubReleaseCheckerService.IsNewerVersion(tag, current));
    }

    [Theory]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.2.1", "1.2.0", true)]
    [InlineData("1.2.0+build", "1.2.0", false)]
    [InlineData("v1.2.0", "1.2.0", false)]
    public void IsNewerVersion_SameOrOlder_ReturnsFalse(string tag, string current, bool expected)
    {
        Assert.Equal(expected, GitHubReleaseCheckerService.IsNewerVersion(tag, current));
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ReturnsParsedRelease()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"tag_name":"v1.2.0","html_url":"https://github.com/x/releases/tag/v1.2.0"}""");
        using var http = new HttpClient(handler);
        using var service = new GitHubReleaseCheckerService(http);

        var release = await service.CheckLatestReleaseAsync();

        Assert.NotNull(release);
        Assert.Equal("v1.2.0", release!.TagName);
        Assert.Equal("https://github.com/x/releases/tag/v1.2.0", release.HtmlUrl);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_OnHttpError_ReturnsNull()
    {
        var handler = StubHttpMessageHandler.Json("", HttpStatusCode.NotFound);
        using var http = new HttpClient(handler);
        using var service = new GitHubReleaseCheckerService(http);

        var release = await service.CheckLatestReleaseAsync();

        Assert.Null(release);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_SetsUserAgent()
    {
        var handler = StubHttpMessageHandler.Json("""{"tag_name":"v1.0.0"}""");
        using var http = new HttpClient(handler);
        using var service = new GitHubReleaseCheckerService(http);

        await service.CheckLatestReleaseAsync();

        Assert.NotEmpty(handler.Requests);
        Assert.Contains("XBVault", handler.Requests[0].Headers.UserAgent.ToString());
    }
}
