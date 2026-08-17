#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace XBVault.Services;

/// <summary>
/// Provides Xbox system-level operations exposed by the Device Portal.
/// </summary>
public interface IXboxSystemService
{
    /// <summary>
    /// Captures the current Xbox screenshot as encoded image bytes.
    /// </summary>
    Task<byte[]?> CaptureScreenshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns raw JSON system information from the Device Portal.
    /// </summary>
    Task<string?> GetSystemInfoAsync();

    /// <summary>
    /// Returns raw JSON crash dump metadata.
    /// </summary>
    Task<string?> GetCrashDumpsAsync();

    /// <summary>
    /// Deletes a crash dump by filename.
    /// </summary>
    Task<bool> DeleteCrashDumpAsync(string filename);

    /// <summary>
    /// Returns raw JSON crash-control settings.
    /// </summary>
    Task<string?> GetCrashControlAsync();

    /// <summary>
    /// Enables or disables crash dump collection.
    /// </summary>
    Task<bool> SetCrashControlAsync(bool enabled);

    /// <summary>
    /// Requests a console restart.
    /// </summary>
    Task<bool> RestartXboxAsync();

    /// <summary>
    /// Requests a console shutdown.
    /// </summary>
    Task<bool> ShutdownXboxAsync();
}
