using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

#pragma warning disable CA5359 // Xbox uses self-signed certs — bypass intentional

namespace XBVault.Services;

public class XboxPerformanceService
{
    private readonly XboxAuthService _auth;

    public XboxPerformanceService(XboxAuthService auth)
    {
        _auth = auth;
    }

    public async Task ConnectPerformanceWsAsync(Action<PerformanceSnapshot> onData, CancellationToken ct)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("ConnectPerformanceWs called but not configured");
            return;
        }

        var ws = new ClientWebSocket();
        try
        {
            var auth = _auth.Http.DefaultRequestHeaders.Authorization;
            if (auth is not null)
                ws.Options.SetRequestHeader("Authorization", $"{auth.Scheme} {auth.Parameter}");

            if (!string.IsNullOrEmpty(_auth.CsrfToken))
                ws.Options.SetRequestHeader("Cookie", $"CSRF-Token={_auth.CsrfToken}");

            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

            var wsUrl = $"{_auth.GetWsBaseUrl()}/api/resourcemanager/systemperf";
            Logger.Info($"WS connecting to {wsUrl}");
            await ws.ConnectAsync(new Uri(wsUrl), ct);
            Logger.Info("WS connected");

            var buffer = new byte[8192];
            var messageBuf = new StringBuilder();

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                messageBuf.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var json = messageBuf.ToString();
                    messageBuf.Clear();

                    var snap = PerformanceSnapshot.Parse(json);
                    if (snap is not null)
                        onData(snap);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("WS cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "WS error");
        }
        finally
        {
            if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None); }
                catch (Exception ex) { Logger.Trace($"WS close error (ignored): {ex.Message}"); }
            }
            ws.Dispose();
            Logger.Info("WS disconnected");
        }
    }
}
