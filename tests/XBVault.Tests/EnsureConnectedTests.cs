using XBVault.Models;
using XBVault.Services;

namespace XBVault.Tests;

public class EnsureConnectedTests
{
    private sealed class FakeAuth : XboxAuthService
    {
        public int TestAttempts;
        public ConnectionTestResult Result = new(true, 200, null);

        public override Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
        {
            TestAttempts++;
            return Task.FromResult(Result);
        }
    }

    private sealed class GatedAuth : XboxAuthService
    {
        public int TestAttempts;
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
        {
            TestAttempts++;
            await Release.Task;
            return new ConnectionTestResult(true, 200, null);
        }
    }

    private const string BaseUrl = "https://127.0.0.1:11443";
    private const string User = "devuser";
    private const string Password = "secret";

    private static FakeAuth ConnectedAuth()
    {
        var auth = new FakeAuth();
        auth.Configure(BaseUrl, User, Password);
        return auth;
    }

    [Fact]
    public async Task EnsureConnected_AlreadyConnected_ReturnsTrueWithoutTest()
    {
        var auth = ConnectedAuth();
        auth.MarkConnected();
        SettingsService.Current.AutoConnect = true;

        var ok = await auth.EnsureConnectedAsync();

        Assert.True(ok);
        Assert.Equal(0, auth.TestAttempts);
    }

    [Fact]
    public async Task EnsureConnected_FlagOff_ReturnsFalseWithoutTest()
    {
        var auth = ConnectedAuth();
        SettingsService.Current.AutoConnect = false;

        var ok = await auth.EnsureConnectedAsync();

        Assert.False(ok);
        Assert.Equal(0, auth.TestAttempts);
    }

    [Fact]
    public async Task EnsureConnected_NotConfigured_ReturnsFalse()
    {
        var auth = new FakeAuth();
        SettingsService.Current.AutoConnect = true;

        var ok = await auth.EnsureConnectedAsync();

        Assert.False(ok);
        Assert.Equal(0, auth.TestAttempts);
    }

    [Fact]
    public async Task EnsureConnected_AfterDisconnect_ReturnsFalse()
    {
        var auth = ConnectedAuth();
        SettingsService.Current.AutoConnect = true;
        auth.MarkConnected();
        auth.Disconnect();

        var ok = await auth.EnsureConnectedAsync();

        Assert.False(ok);
        Assert.Equal(0, auth.TestAttempts);
    }

    [Fact]
    public async Task EnsureConnected_ManualReconnect_ClearsDisconnectFlag()
    {
        var auth = ConnectedAuth();
        SettingsService.Current.AutoConnect = true;
        auth.MarkConnected();
        auth.Disconnect();
        auth.Configure(BaseUrl, User, Password);
        auth.MarkConnected();

        var ok = await auth.EnsureConnectedAsync();

        Assert.True(ok);
        Assert.Equal(0, auth.TestAttempts);
    }

    [Fact]
    public async Task EnsureConnected_Success_MarksConnected()
    {
        var auth = ConnectedAuth();
        SettingsService.Current.AutoConnect = true;
        var events = new List<bool>();
        auth.ConnectionChanged += c => events.Add(c);

        var ok = await auth.EnsureConnectedAsync();

        Assert.True(ok);
        Assert.True(auth.IsConnected);
        Assert.Equal(1, auth.TestAttempts);
        Assert.Contains(true, events);
    }

    [Fact]
    public async Task EnsureConnected_Failure_SetsCooldownBlockingRetry()
    {
        var auth = ConnectedAuth();
        SettingsService.Current.AutoConnect = true;
        auth.Result = new ConnectionTestResult(false, 502, "HTTP 502");

        var first = await auth.EnsureConnectedAsync();
        var second = await auth.EnsureConnectedAsync();

        Assert.False(first);
        Assert.False(second);
        Assert.False(auth.IsConnected);
        Assert.Equal(1, auth.TestAttempts);
    }

    [Fact]
    public async Task EnsureConnected_ConcurrentCalls_RunSingleTest()
    {
        var auth = new GatedAuth();
        auth.Configure(BaseUrl, User, Password);
        SettingsService.Current.AutoConnect = true;

        var t1 = auth.EnsureConnectedAsync();
        var t2 = auth.EnsureConnectedAsync();

        // Let the first caller enter TestConnectionAsync before releasing
        await Task.Delay(100);
        Assert.Equal(1, auth.TestAttempts);

        auth.Release.SetResult();

        var results = await Task.WhenAll(t1, t2);
        Assert.True(results[0]);
        Assert.True(results[1]);
        Assert.Equal(1, auth.TestAttempts);
    }
}
