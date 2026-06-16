using System.Collections.ObjectModel;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class LogsViewModel
{
    public ObservableCollection<LogEntry> Logs { get; } = Logger.Entries;

    public LogsViewModel()
    {
        // nothing else; UI binds directly to Logger.Entries
    }
}
