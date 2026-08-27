using System.Net;
using System.Reflection;
using System.Text;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class XboxNetworkServiceTests
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
    public async Task GetNetworkConfig_ReturnsBody_OnSuccess()
    {
        var handler = JsonFor("/api/networking/ipconfig", "{\"ip\":\"10.0.0.50\"}");
        var svc = new XboxNetworkService(CreateAuth(handler));

        var body = await svc.GetNetworkConfigAsync();

        Assert.Equal("{\"ip\":\"10.0.0.50\"}", body);
    }

    [Fact]
    public async Task GetNetworkConfig_ReturnsNull_OnError()
    {
        var handler = JsonFor("/api/networking/ipconfig", "boom", HttpStatusCode.InternalServerError);
        var svc = new XboxNetworkService(CreateAuth(handler));

        var body = await svc.GetNetworkConfigAsync();

        Assert.Null(body);
    }

    [Fact]
    public async Task GetWifiInterfaces_ReturnsBody_OnSuccess()
    {
        var handler = JsonFor("/api/wifi/interfaces", "{\"Interfaces\":[]}");
        var svc = new XboxNetworkService(CreateAuth(handler));

        var body = await svc.GetWifiInterfacesAsync();

        Assert.Equal("{\"Interfaces\":[]}", body);
    }

    [Fact]
    public async Task GetWifiInterfaces_ReturnsNull_OnError()
    {
        var handler = JsonFor("/api/wifi/interfaces", "boom", HttpStatusCode.BadGateway);
        var svc = new XboxNetworkService(CreateAuth(handler));

        var body = await svc.GetWifiInterfacesAsync();

        Assert.Null(body);
    }

    [Fact]
    public async Task GetWifiNetworks_AddsInterfaceQuery_PassesThrough()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal("/api/wifi/networks?interface=3ac3c592-a98e-4a2f-9f6c-030f9c01a012", request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"Networks\":[]}", Encoding.UTF8, "application/json") };
        });
        var svc = new XboxNetworkService(CreateAuth(handler));

        var body = await svc.GetWifiNetworksAsync("3ac3c592-a98e-4a2f-9f6c-030f9c01a012");

        Assert.Equal("{\"Networks\":[]}", body);
    }

    [Fact]
    public async Task GetWifiNetworks_ReturnsNull_OnError()
    {
        var handler = JsonFor("/api/wifi/networks", "boom", HttpStatusCode.BadRequest);
        var svc = new XboxNetworkService(CreateAuth(handler));

        var body = await svc.GetWifiNetworksAsync("iface0");

        Assert.Null(body);
    }

    [Fact]
    public async Task AllMethods_ReturnNull_WhenNotConfigured()
    {
        var auth = new XboxAuthService();
        var svc = new XboxNetworkService(auth);

        Assert.Null(await svc.GetNetworkConfigAsync());
        Assert.Null(await svc.GetWifiInterfacesAsync());
        Assert.Null(await svc.GetWifiNetworksAsync("x"));
    }
}
