using System.Net;
using System.Reflection;
using System.Text;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class XboxSystemServiceTests
{
    private static XboxAuthService CreateAuth(StubHttpMessageHandler handler)
    {
        var auth = new XboxAuthService();
        auth.Configure("http://xbox.local:11443", "DevToolsUser", "pw");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://xbox.local:11443") };
        var flag = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(XboxAuthService).GetField("_http", flag)!.SetValue(auth, http);
        typeof(XboxAuthService).GetField("_transferHttp", flag)!.SetValue(auth, http);
        return auth;
    }

    private static XboxAuthService CreateUnconfiguredAuth() => new();

    private static StubHttpMessageHandler OkFor(Func<HttpRequestMessage, bool> match, string body = "ok",
        HttpStatusCode status = HttpStatusCode.OK) =>
        new(request =>
        {
            if (match(request))
                return new HttpResponseMessage(status)
                { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    [Fact]
    public async Task CaptureScreenshot_ReturnsBytes_OnSuccess()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.StartsWith("/ext/screenshot?", request.RequestUri!.PathAndQuery);
            Assert.Contains("download=true", request.RequestUri.Query);
            Assert.Contains("hdr=false", request.RequestUri.Query);
            Assert.Contains("time=", request.RequestUri.Query);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        });
        var svc = new XboxSystemService(CreateAuth(handler))
        {
            ScreenshotRetryDelay = TimeSpan.Zero
        };

        var bytes = await svc.CaptureScreenshotAsync();

        Assert.Equal(payload, bytes);
    }

    [Fact]
    public async Task CaptureScreenshot_RetriesAfterFailure_ThenSucceeds()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            attempts++;
            return attempts < 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9]) };
        });
        var svc = new XboxSystemService(CreateAuth(handler))
        {
            ScreenshotRetryDelay = TimeSpan.Zero,
            ScreenshotMaxRetries = 5
        };

        var bytes = await svc.CaptureScreenshotAsync();

        Assert.Equal(new byte[] { 9 }, bytes);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task CaptureScreenshot_ReturnsNull_AfterMaxRetries()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        var svc = new XboxSystemService(CreateAuth(handler))
        {
            ScreenshotRetryDelay = TimeSpan.Zero,
            ScreenshotMaxRetries = 3
        };

        var bytes = await svc.CaptureScreenshotAsync();

        Assert.Null(bytes);
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task CaptureScreenshot_ReturnsNull_WhenNotConfigured()
    {
        var handler = OkFor(_ => true);
        var svc = new XboxSystemService(CreateUnconfiguredAuth())
        {
            ScreenshotRetryDelay = TimeSpan.Zero
        };

        var bytes = await svc.CaptureScreenshotAsync();

        Assert.Null(bytes);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetSystemInfo_ReturnsBody_OnSuccess()
    {
        var handler = OkFor(r => r.RequestUri!.PathAndQuery.StartsWith("/api/systeminfo"), "{\"systinfo\":true}");
        var svc = new XboxSystemService(CreateAuth(handler));

        var body = await svc.GetSystemInfoAsync();

        Assert.Equal("{\"systinfo\":true}", body);
    }

    [Fact]
    public async Task GetSystemInfo_FallsBackToOsInfo_WhenPrimaryFails()
    {
        var handler = OkFor(r => r.RequestUri!.PathAndQuery.StartsWith("/api/os/info"), "{\"osinfo\":true}");
        var svc = new XboxSystemService(CreateAuth(handler));

        var body = await svc.GetSystemInfoAsync();

        Assert.Equal("{\"osinfo\":true}", body);
        Assert.Contains(handler.Requests, r => r.RequestUri!.PathAndQuery.StartsWith("/api/os/info"));
    }

    [Fact]
    public async Task GetSystemInfo_ReturnsNull_WhenAllEndpointsFail()
    {
        var handler = OkFor(_ => false);
        var svc = new XboxSystemService(CreateAuth(handler));

        var body = await svc.GetSystemInfoAsync();

        Assert.Null(body);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetCrashDumps_ReturnsBody_OnSuccess()
    {
        var handler = OkFor(r => r.RequestUri!.PathAndQuery.StartsWith("/api/app/debug/crashdump"), "{\"dumps\":[]}");
        var svc = new XboxSystemService(CreateAuth(handler));

        var body = await svc.GetCrashDumpsAsync();

        Assert.Equal("{\"dumps\":[]}", body);
    }

    [Fact]
    public async Task GetCrashDumps_ReturnsNull_OnError()
    {
        var handler = OkFor(_ => false);
        var svc = new XboxSystemService(CreateAuth(handler));

        var body = await svc.GetCrashDumpsAsync();

        Assert.Null(body);
    }

    [Fact]
    public async Task DeleteCrashDump_ReturnsTrue_AndEncodesFilename()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("/api/app/debug/crashdump/foo%20bar.dmp", request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.DeleteCrashDumpAsync("foo bar.dmp");

        Assert.True(ok);
    }

    [Fact]
    public async Task DeleteCrashDump_ReturnsFalse_OnError()
    {
        var handler = new StubHttpMessageHandler(request => request.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK));
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.DeleteCrashDumpAsync("x.dmp");

        Assert.False(ok);
    }

    [Fact]
    public async Task GetCrashControl_ReturnsBody_OnSuccess()
    {
        var handler = OkFor(r => r.RequestUri!.PathAndQuery.StartsWith("/api/app/debug/crashcontrol"), "{true}");
        var svc = new XboxSystemService(CreateAuth(handler));

        var body = await svc.GetCrashControlAsync();

        Assert.Equal("{true}", body);
    }

    [Fact]
    public async Task SetCrashControl_PostsEnabledForm_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/app/debug/crashcontrol", request.RequestUri!.PathAndQuery);
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Equal("CrashDumpEnabled=true", body);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.SetCrashControlAsync(true);

        Assert.True(ok);
    }

    [Fact]
    public async Task SetCrashControl_PostsDisabledForm_OnFalse()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Equal("CrashDumpEnabled=false", body);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.SetCrashControlAsync(false);

        Assert.True(ok);
    }

    [Fact]
    public async Task SetCrashControl_ReturnsFalse_OnError()
    {
        var handler = new StubHttpMessageHandler(request => request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
            : new HttpResponseMessage(HttpStatusCode.OK));
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.SetCrashControlAsync(true);

        Assert.False(ok);
    }

    [Fact]
    public async Task RestartXbox_PostsToControlRestart_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/control/restart", request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.RestartXboxAsync();

        Assert.True(ok);
    }

    [Fact]
    public async Task ShutdownXbox_PostsToControlShutdown_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/control/shutdown", request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.ShutdownXboxAsync();

        Assert.True(ok);
    }

    [Fact]
    public async Task RestartXbox_ReturnsFalse_OnError()
    {
        var handler = new StubHttpMessageHandler(request => request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK));
        var svc = new XboxSystemService(CreateAuth(handler));

        var ok = await svc.RestartXboxAsync();

        Assert.False(ok);
    }

    [Fact]
    public async Task Controls_ReturnFalse_WhenNotConfigured()
    {
        var svc = new XboxSystemService(CreateUnconfiguredAuth());

        Assert.False(await svc.RestartXboxAsync());
        Assert.False(await svc.ShutdownXboxAsync());
        Assert.False(await svc.SetCrashControlAsync(true));
        Assert.False(await svc.DeleteCrashDumpAsync("x.dmp"));
        Assert.Null(await svc.GetCrashControlAsync());
        Assert.Null(await svc.GetCrashDumpsAsync());
        Assert.Null(await svc.GetSystemInfoAsync());
    }
}
