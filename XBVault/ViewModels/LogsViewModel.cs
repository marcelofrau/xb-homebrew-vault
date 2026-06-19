using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _autoScroll = true;

    public ObservableCollection<LogEntry> Logs { get; } = Logger.Entries;

    public LogsViewModel()
    {
        Logger.Debug("LogsViewModel initialized (binding to Logger.Entries)");
    }
}
