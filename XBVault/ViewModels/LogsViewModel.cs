using System.Collections.ObjectModel;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class LogsViewModel
{
    public ObservableCollection<LogEntry> Logs { get; } = Logger.Entries;

    public LogsViewModel()
    {
        Logger.Debug("LogsViewModel initialized (binding to Logger.Entries)");
    }
}
