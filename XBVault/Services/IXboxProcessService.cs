using System.Threading.Tasks;

namespace XBVault.Services;

public interface IXboxProcessService
{
    Task<string?> GetRunningTitleAsync();
    Task<string?> GetProcessesAsync();
    Task<bool> KillProcessAsync(int pid);
}
