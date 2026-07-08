using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private const double DefaultLogFontSize = 15;
    private const double MinLogFontSize = 11;
    private const double MaxLogFontSize = 24;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseLogFontSizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseLogFontSizeCommand))]
    private double _logFontSize;

    public ObservableCollection<LogEntry> Logs { get; } = Logger.Entries;

    public LogsViewModel()
    {
        LogFontSize = ClampLogFontSize(SettingsService.Current.LogFontSize);
        Logger.Debug("LogsViewModel initialized (binding to Logger.Entries)");
    }

    partial void OnLogFontSizeChanged(double value)
    {
        var clamped = ClampLogFontSize(value);
        if (Math.Abs(clamped - value) > double.Epsilon)
        {
            LogFontSize = clamped;
            return;
        }

        SettingsService.Current.LogFontSize = clamped;
        SettingsService.Save();
    }

    [RelayCommand(CanExecute = nameof(CanIncreaseLogFontSize))]
    private void IncreaseLogFontSize()
    {
        LogFontSize += 1;
    }

    private bool CanIncreaseLogFontSize() => LogFontSize < MaxLogFontSize;

    [RelayCommand(CanExecute = nameof(CanDecreaseLogFontSize))]
    private void DecreaseLogFontSize()
    {
        LogFontSize -= 1;
    }

    private bool CanDecreaseLogFontSize() => LogFontSize > MinLogFontSize;

    private static double ClampLogFontSize(double value)
    {
        if (double.IsNaN(value) || value <= 0)
            return DefaultLogFontSize;

        return Math.Clamp(value, MinLogFontSize, MaxLogFontSize);
    }
}
