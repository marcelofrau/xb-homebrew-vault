using System;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public interface IXboxPerformanceService
{
    Task ConnectPerformanceWsAsync(Action<PerformanceSnapshot> onData, CancellationToken ct);
}
