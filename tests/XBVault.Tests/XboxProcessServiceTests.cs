using System.Net;
using System.Reflection;
using System.Text;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class XboxProcessServiceTests
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

    private static StubHttpMessageHandler JsonFor(string prefix, string body,
        HttpStatusCode status = HttpStatusCode.OK) =>
        new(request => request.RequestUri!.PathAndQuery.StartsWith(prefix)
            ? new HttpResponseMessage(status)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound));

    [Fact]
    public async Task GetRunningTitle_ReturnsPfn_WhenPresent()
    {
        var handler = JsonFor("/ext/app/runningtitle",
            "{\"PackageFullName\":\"Gen1Recomp_0.2.29_x64__8wekyb3d8bbwe\"}");
        var svc = new XboxProcessService(CreateAuth(handler));

        var pfn = await svc.GetRunningTitleAsync();

        Assert.Equal("Gen1Recomp_0.2.29_x64__8wekyb3d8bbwe", pfn);
    }

    [Fact]
    public async Task GetRunningTitle_ReturnsNull_WhenEmpty()
    {
        var handler = JsonFor("/ext/app/runningtitle", "{\"PackageFullName\":\"\"}");
        var svc = new XboxProcessService(CreateAuth(handler));

        var pfn = await svc.GetRunningTitleAsync();

        Assert.Null(pfn);
    }

    [Fact]
    public async Task GetRunningTitle_ReturnsNull_WhenPropertyMissing()
    {
        var handler = JsonFor("/ext/app/runningtitle", "{}");
        var svc = new XboxProcessService(CreateAuth(handler));

        var pfn = await svc.GetRunningTitleAsync();

        Assert.Null(pfn);
    }

    [Fact]
    public async Task GetProcesses_ReturnsBody_OnSuccess()
    {
        var handler = JsonFor("/api/resourcemanager/processes", "{\"Processes\":[]}");
        var svc = new XboxProcessService(CreateAuth(handler));

        var body = await svc.GetProcessesAsync();

        Assert.Equal("{\"Processes\":[]}", body);
    }

    [Fact]
    public async Task GetProcesses_ReturnsNull_OnError()
    {
        var handler = JsonFor("/api/resourcemanager/processes", "boom", HttpStatusCode.InternalServerError);
        var svc = new XboxProcessService(CreateAuth(handler));

        var body = await svc.GetProcessesAsync();

        Assert.Null(body);
    }

    [Fact]
    public async Task KillProcess_DeletesProcess_AndReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("/api/resourcemanager/process?pid=1234", request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var svc = new XboxProcessService(CreateAuth(handler));

        var ok = await svc.KillProcessAsync(1234);

        Assert.True(ok);
    }

    [Fact]
    public async Task KillProcess_ReturnsFalse_OnError()
    {
        var handler = new StubHttpMessageHandler(request =>
            request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK));
        var svc = new XboxProcessService(CreateAuth(handler));

        var ok = await svc.KillProcessAsync(999);

        Assert.False(ok);
    }

    [Fact]
    public async Task AllMethods_HandleNotConfigured_Safely()
    {
        var auth = new XboxAuthService();
        var svc = new XboxProcessService(auth);

        Assert.Null(await svc.GetRunningTitleAsync());
        Assert.Null(await svc.GetProcessesAsync());
        Assert.False(await svc.KillProcessAsync(1));
    }
}
