using XBVault.Services;

namespace XBVault.Models;

public class FilteredLogEntry
{
    public LogEntry Entry { get; set; } = null!;
    public bool IsMatch { get; set; }
}
