using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using XBVault.Models;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class XboxPerformanceServiceTests
{
    private const string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private const string BigJson =
        "{\"CpuLoad\":42.5,\"AvailablePages\":100,\"TotalPages\":200,\"CommittedPages\":150,\"PageSize\":4096," +
        "\"IOReadSpeed\":3,\"IOWriteSpeed\":4,\"IOOtherSpeed\":5," +
        "\"GPUData\":{\"AvailableAdapters\":[{\"DedicatedMemory\":6,\"DedicatedMemoryUsed\":7,\"SystemMemory\":8,\"SystemMemoryUsed\":9,\"EnginesUtilization\":[55.0]}]}," +
        "\"NetworkingData\":{\"NetworkInBytes\":10,\"NetworkOutBytes\":11}}";

    private static XboxAuthService CreateAuth(int port)
    {
        var auth = new XboxAuthService();
        auth.Configure($"http://127.0.0.1:{port}", "DevToolsUser", "pw");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var flag = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(XboxAuthService).GetField("_http", flag)!.SetValue(auth, http);
        typeof(XboxAuthService).GetField("_transferHttp", flag)!.SetValue(auth, http);
        return auth;
    }

    [Fact]
    public async Task ConnectPerformanceWs_Returns_WhenNotConfigured()
    {
        var auth = new XboxAuthService();
        var svc = new XboxPerformanceService(auth);
        var calls = 0;

        await svc.ConnectPerformanceWsAsync(_ => calls++, CancellationToken.None);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ConnectPerformanceWs_CancelledToken_ReturnsCleanly()
    {
        var svc = new XboxPerformanceService(CreateAuth(1));
        var calls = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await svc.ConnectPerformanceWsAsync(_ => calls++, cts.Token);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ConnectPerformanceWs_ReceivesSnapshot_AndExits()
    {
        const string json =
            "{\"CpuLoad\":42.5,\"AvailablePages\":100,\"TotalPages\":200,\"CommittedPages\":150,\"PageSize\":4096," +
            "\"IOReadSpeed\":3,\"IOWriteSpeed\":4,\"IOOtherSpeed\":5," +
            "\"GPUData\":{\"AvailableAdapters\":[{\"DedicatedMemory\":6,\"DedicatedMemoryUsed\":7,\"SystemMemory\":8,\"SystemMemoryUsed\":9,\"EnginesUtilization\":[55.0]}]}," +
            "\"NetworkingData\":{\"NetworkInBytes\":10,\"NetworkOutBytes\":11}}";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new FakeWsServer();
        var svc = new XboxPerformanceService(CreateAuth(server.Port));
        var snaps = new List<PerformanceSnapshot>();
        Assert.NotNull(PerformanceSnapshot.Parse(json));

        var recv = svc.ConnectPerformanceWsAsync(snaps.Add, cts.Token);
        var accept = server.AcceptAndHandshakeAsync(cts.Token);
        await accept;
        await server.SendTextAsync(json);
        await server.SendCloseAsync();
        await server.ReadUntilRemoteCloseAsync(cts.Token);
        await recv;

        var snap = Assert.Single(snaps);
        Assert.Equal(42.5, snap.CpuLoad);
        Assert.Equal(55.0, snap.GpuUsage);
        Assert.Equal(200, snap.TotalPages);
        Assert.Equal(150, snap.CommittedPages);
        Assert.Equal(4096, snap.PageSize);
        Assert.Equal(6, snap.DedicatedMemory);
        Assert.Equal(9, snap.SystemMemoryUsed);
        Assert.Equal(10, snap.NetworkInBytes);
        Assert.Equal(11, snap.NetworkOutBytes);
        Assert.Equal(12, snap.IoTotalSpeed);
    }

    [Fact]
    public async Task ConnectPerformanceWs_BuffersFragmentedMessages_IntoSingleSnapshot()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new FakeWsServer();
        var svc = new XboxPerformanceService(CreateAuth(server.Port));
        var snaps = new List<PerformanceSnapshot>();

        var accept = server.AcceptAndHandshakeAsync(cts.Token);
        var recv = svc.ConnectPerformanceWsAsync(snaps.Add, cts.Token);
        await accept;
        await server.SendFragmentAsync(fin: false, opcode: 0x1, payload: "{\"CpuLoad\":12.5,");
        await server.SendFragmentAsync(fin: true, opcode: 0x0, payload: "\"PageSize\":4096,\"TotalPages\":512}");
        await server.SendCloseAsync();
        await server.ReadUntilRemoteCloseAsync(cts.Token);
        await recv;

        var snap = Assert.Single(snaps);
        Assert.Equal(12.5, snap.CpuLoad);
        Assert.Equal(512, snap.TotalPages);
    }

    [Fact]
    public async Task ConnectPerformanceWs_InvalidJson_IsIgnored()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new FakeWsServer();
        var svc = new XboxPerformanceService(CreateAuth(server.Port));
        var snaps = new List<PerformanceSnapshot>();

        var accept = server.AcceptAndHandshakeAsync(cts.Token);
        var recv = svc.ConnectPerformanceWsAsync(snaps.Add, cts.Token);
        await accept;
        await server.SendTextAsync("not-json");
        await server.SendCloseAsync();
        await server.ReadUntilRemoteCloseAsync(cts.Token);
        await recv;

        Assert.Empty(snaps);
    }

    private sealed class FakeWsServer : IDisposable
    {
        private readonly TcpListener _listener;
        private Socket _socket = null!;
        private NetworkStream _stream = null!;
        private bool _disposed;

        public FakeWsServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public int Port { get; }

        public async Task AcceptAndHandshakeAsync(CancellationToken ct)
        {
            var tcp = await _listener.AcceptTcpClientAsync(ct);
            _socket = tcp.Client;
            _stream = new NetworkStream(_socket, ownsSocket: false);

            var payload = new List<byte>();
            var buf = new byte[1024];
            while (!ContainsHeaderTerminator(payload))
            {
                int n = await _stream.ReadAsync(buf.AsMemory(0, buf.Length), ct);
                if (n == 0) throw new IOException("WS client disconnected during handshake");
                for (int i = 0; i < n; i++)
                    payload.Add(buf[i]);
            }

            var header = Encoding.ASCII.GetString(payload.ToArray());
            var key = header
                .Split('\n')
                .Select(l => l.Trim())
                .First(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))["Sec-WebSocket-Key:".Length..].Trim();

            byte[] accept;
            using (var sha1 = SHA1.Create())
                accept = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + WsGuid));

            var response = $"HTTP/1.1 101 Switching Protocols\r\n" +
                           $"Upgrade: websocket\r\n" +
                           $"Connection: Upgrade\r\n" +
                           $"Sec-WebSocket-Accept: {Convert.ToBase64String(accept)}\r\n\r\n";
            await _stream.WriteAsync(Encoding.ASCII.GetBytes(response), ct);
            await _stream.FlushAsync(ct);
        }

        private static readonly byte[] Terminator = "\r\n\r\n"u8.ToArray();

        private static bool ContainsHeaderTerminator(List<byte> data)
        {
            if (data.Count < 4) return false;
            for (int i = data.Count - 4; i >= 0; i--)
            {
                if (data[i] == Terminator[0] && data[i + 1] == Terminator[1] &&
                    data[i + 2] == Terminator[2] && data[i + 3] == Terminator[3])
                    return true;
            }
            return false;
        }

        public ValueTask SendTextAsync(string payload) =>
            SendFrameAsync(fin: true, opcode: 0x1, payload);

        public ValueTask SendFragmentAsync(bool fin, byte opcode, string payload) =>
            SendFrameAsync(fin, opcode, payload);

        private ValueTask SendFrameAsync(bool fin, byte opcode, string payload)
        {
            var text = Encoding.UTF8.GetBytes(payload);
            var header = new List<byte> { (byte)((fin ? 0x80 : 0x00) | opcode) };
            if (text.Length < 126)
            {
                header.Add((byte)text.Length);
            }
            else if (text.Length <= ushort.MaxValue)
            {
                header.Add(126);
                header.Add((byte)(text.Length >> 8));
                header.Add((byte)text.Length);
            }
            else
            {
                header.Add(127);
                var len = (ulong)text.Length;
                for (int i = 7; i >= 0; i--)
                    header.Add((byte)(len >> (8 * i)));
            }

            var frame = new byte[header.Count + text.Length];
            header.CopyTo(frame, 0);
            Buffer.BlockCopy(text, 0, frame, header.Count, text.Length);
            return _stream.WriteAsync(frame, default);
        }

        public ValueTask SendCloseAsync()
        {
            var frame = new byte[] { 0x88, 0x02, 0x03, 0xE8 };
            return _stream.WriteAsync(frame, default);
        }

        public async Task ReadUntilRemoteCloseAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                var buf = new byte[4096];
                while (!ct.IsCancellationRequested)
                {
                    int n = await _stream.ReadAsync(buf.AsMemory(0, buf.Length), ct);
                    if (n == 0) return;
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stream.Dispose();
            _socket.Dispose();
            _listener.Stop();
        }
    }
}
