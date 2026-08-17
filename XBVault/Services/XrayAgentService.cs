#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public class XrayAgentService : IDisposable
{
    private const int ScanTimeoutMs = 2000;
    private const int ReadTimeoutMs = 3000;
    private const int MinPort = 9000;
    private const int MaxPort = 9009;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private Task? _receiveTask;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly int _scanConcurrency = 5;

    public event Action<XrayLogMessage>? LogReceived;
    public event Action<XrayReplResult>? ReplResultReceived;
    public event Action<XrayCommandResult>? CommandResultReceived;
    public event Action<string>? Disconnected;

    public bool IsConnected => _client?.Connected == true;

    public async Task<List<XrayAgentInfo>> ScanAsync(string host, int timeoutMs = ScanTimeoutMs)
    {
        var semaphore = new SemaphoreSlim(_scanConcurrency);
        var tasks = Enumerable.Range(MinPort, MaxPort - MinPort + 1)
            .Select(port => ProbePortAsync(host, port, timeoutMs, semaphore));
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).ToList()!;
    }

    private static async Task<XrayAgentInfo?> ProbePortAsync(string host, int port, int timeoutMs, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            using var scanCts = new CancellationTokenSource(timeoutMs);
            using var client = new TcpClient(AddressFamily.InterNetwork);
            client.NoDelay = true;

            await client.ConnectAsync(host, port, scanCts.Token);

            if (!client.Connected) return null;

            using var stream = client.GetStream();
            stream.ReadTimeout = ReadTimeoutMs;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var line = await ReadLineWithTimeoutAsync(reader, ReadTimeoutMs);
            if (line is null) return null;

            var handshake = JsonSerializer.Deserialize<XrayHandshake>(line);
            if (handshake?.Event == "handshake" && handshake.Payload is not null)
            {
                return new XrayAgentInfo
                {
                    Port = port,
                    AppName = handshake.Payload.AppName,
                    AppId = handshake.Payload.AppId,
                    Version = handshake.Payload.Version,
                    Language = handshake.Payload.Language,
                    Environment = handshake.Payload.Environment,
                    ProtocolVersion = handshake.Payload.ProtocolVersion,
                    Capabilities = handshake.Payload.Capabilities ?? []
                };
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Trace($"Port {port}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<bool> ConnectAsync(string host, int port, int timeoutMs = 10000)
    {
        Disconnect();

        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            _client = new TcpClient(AddressFamily.InterNetwork);
            _client.NoDelay = true;
            await _client.ConnectAsync(host, port, cts.Token);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);

            // Read initial handshake
            var handshakeLine = await _reader.ReadLineAsync();
            if (handshakeLine is null)
            {
                Logger.Warn("Xray connect: no handshake received");
                Disconnect();
                return false;
            }

            Logger.Info($"Xray connected to {host}:{port} — handshake: {Truncate(handshakeLine, 200)}");

            _cts = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Xray connect failed to {host}:{port}");
            Disconnect();
            return false;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader is not null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line is null) break;

                if (string.IsNullOrWhiteSpace(line)) continue;

                XrayLogMessage? logMsg = null;
                XrayReplResult? replMsg = null;
                XrayCommandResult? cmdMsg = null;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var eventName = doc.RootElement.GetProperty("event").GetString();

                    switch (eventName)
                    {
                        case "log":
                            logMsg = JsonSerializer.Deserialize<XrayLogMessage>(line);
                            break;
                        case "repl_result":
                            replMsg = JsonSerializer.Deserialize<XrayReplResult>(line);
                            break;
                        case "command_result":
                            cmdMsg = JsonSerializer.Deserialize<XrayCommandResult>(line);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Xray receive parse error: {ex.Message}");
                }

                if (logMsg is not null)
                    LogReceived?.Invoke(logMsg);
                if (replMsg is not null)
                    ReplResultReceived?.Invoke(replMsg);
                if (cmdMsg is not null)
                    CommandResultReceived?.Invoke(cmdMsg);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.Warn($"Xray receive loop ended: {ex.Message}");
        }
        finally
        {
            Disconnected?.Invoke("Connection closed");
            Cleanup();
        }
    }

    public async Task SendReplEvalAsync(string script, string id)
    {
        var payload = JsonSerializer.Serialize(new { id, script });
        var json = $"{{\"event\":\"repl_eval\",\"payload\":{payload}}}";
        Logger.Debug($"Xray send REPL: {Truncate(json, 300)}");
        await SendAsync(json);
    }

    public async Task SendCommandAsync(string command, object? payload = null)
    {
        var payloadJson = payload is not null ? JsonSerializer.Serialize(payload) : "{}";
        var json = $"{{\"event\":\"{command}\",\"payload\":{payloadJson}}}";
        Logger.Debug($"Xray send cmd: {Truncate(json, 300)}");
        await SendAsync(json);
    }

    private async Task SendAsync(string json)
    {
        if (_stream is null)
        {
            Logger.Warn("Xray send: no stream");
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _sendLock.WaitAsync();
        try
        {
            await _stream.WriteAsync(bytes);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Xray send failed");
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        Cleanup();
    }

    private void Cleanup()
    {
        _reader?.Dispose();
        _reader = null;
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        Disconnect();
        _sendLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, int timeoutMs)
    {
        var readTask = reader.ReadLineAsync();
        if (await Task.WhenAny(readTask, Task.Delay(timeoutMs)) == readTask)
            return readTask.Result;
        return null;
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";
}
