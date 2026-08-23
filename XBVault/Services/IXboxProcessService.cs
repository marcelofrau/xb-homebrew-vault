#nullable enable
using System.Threading.Tasks;

namespace XBVault.Services;

/// <summary>
/// Provides process and foreground-title operations exposed by the Xbox Device Portal.
/// </summary>
public interface IXboxProcessService
{
    /// <summary>
    /// Returns raw JSON describing the foreground running title.
    /// </summary>
    Task<string?> GetRunningTitleAsync();

    /// <summary>
    /// Returns raw JSON listing running processes.
    /// </summary>
    Task<string?> GetProcessesAsync();

    /// <summary>
    /// Requests termination of a process by process id.
    /// </summary>
    Task<bool> KillProcessAsync(int pid);
}
