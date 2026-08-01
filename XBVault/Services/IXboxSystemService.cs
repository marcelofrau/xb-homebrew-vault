using System.Threading;
using System.Threading.Tasks;

namespace XBVault.Services;

public interface IXboxSystemService
{
    Task<byte[]?> CaptureScreenshotAsync(CancellationToken ct = default);
    Task<string?> GetSystemInfoAsync();
    Task<string?> GetCrashDumpsAsync();
    Task<bool> DeleteCrashDumpAsync(string filename);
    Task<string?> GetCrashControlAsync();
    Task<bool> SetCrashControlAsync(bool enabled);
    Task<bool> RestartXboxAsync();
    Task<bool> ShutdownXboxAsync();
}
