using System;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

/// <summary>
/// Streams Xbox performance telemetry over the Device Portal WebSocket endpoint.
/// </summary>
public interface IXboxPerformanceService
{
    /// <summary>
    /// Opens the performance WebSocket and invokes <paramref name="onData"/> for each parsed snapshot.
    /// </summary>
    Task ConnectPerformanceWsAsync(Action<PerformanceSnapshot> onData, CancellationToken ct);
}
