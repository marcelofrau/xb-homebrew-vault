using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
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

    [ObservableProperty]
    private bool _isFilterVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterMatchText))]
    private string _filterText = "";

    [ObservableProperty]
    private int _filterLinesAbove = 3;

    [ObservableProperty]
    private int _filterLinesBelow = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterMatchText))]
    private int _filterMatchCount;

    public string FilterMatchText =>
        string.IsNullOrEmpty(FilterText) ? "" : $"{FilterMatchCount} matches";

    public ObservableCollection<LogEntry> Logs { get; } = Logger.Entries;
    public ObservableCollection<FilteredLogEntry> FilteredLogs { get; } = new();

    public LogsViewModel()
    {
        LogFontSize = ClampLogFontSize(SettingsService.Current.LogFontSize);
        Logger.Debug("LogsViewModel initialized (binding to Logger.Entries)");
        Logs.CollectionChanged += (_, _) => RebuildFilteredEntries();
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

    partial void OnFilterTextChanged(string value) => RebuildFilteredEntries();
    partial void OnFilterLinesAboveChanged(int value) => RebuildFilteredEntries();
    partial void OnFilterLinesBelowChanged(int value) => RebuildFilteredEntries();

    [RelayCommand]
    private void ToggleFilter()
    {
        IsFilterVisible = !IsFilterVisible;
    }

    [RelayCommand]
    private void CloseFilter()
    {
        IsFilterVisible = false;
    }

    [RelayCommand]
    private void IncrementFilterLinesAbove()
    {
        if (FilterLinesAbove < 10)
            FilterLinesAbove++;
    }

    [RelayCommand]
    private void DecrementFilterLinesAbove()
    {
        if (FilterLinesAbove > 0)
            FilterLinesAbove--;
    }

    [RelayCommand]
    private void IncrementFilterLinesBelow()
    {
        if (FilterLinesBelow < 10)
            FilterLinesBelow++;
    }

    [RelayCommand]
    private void DecrementFilterLinesBelow()
    {
        if (FilterLinesBelow > 0)
            FilterLinesBelow--;
    }

    private void RebuildFilteredEntries()
    {
        FilteredLogs.Clear();
        FilterMatchCount = 0;

        var filter = FilterText?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            foreach (var e in Logs)
                FilteredLogs.Add(new FilteredLogEntry { Entry = e, IsMatch = false });
            return;
        }

        var above = FilterLinesAbove;
        var below = FilterLinesBelow;
        var matchRanges = new List<(int start, int end)>();

        for (int i = 0; i < Logs.Count; i++)
        {
            if (Logs[i].Message.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                var start = Math.Max(0, i - above);
                var end = Math.Min(Logs.Count - 1, i + below);
                matchRanges.Add((start, end));
            }
        }

        if (matchRanges.Count == 0) return;

        var merged = new List<(int start, int end)> { matchRanges[0] };
        for (int i = 1; i < matchRanges.Count; i++)
        {
            if (matchRanges[i].start <= merged[^1].end + 1)
                merged[^1] = (merged[^1].start, Math.Max(merged[^1].end, matchRanges[i].end));
            else
                merged.Add(matchRanges[i]);
        }

        var directMatch = new HashSet<int>();
        for (int i = 0; i < Logs.Count; i++)
        {
            if (Logs[i].Message.Contains(filter, StringComparison.OrdinalIgnoreCase))
                directMatch.Add(i);
        }

        foreach (var (start, end) in merged)
        {
            for (int i = start; i <= end; i++)
            {
                FilteredLogs.Add(new FilteredLogEntry
                {
                    Entry = Logs[i],
                    IsMatch = directMatch.Contains(i)
                });
            }
        }

        FilterMatchCount = directMatch.Count;
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
