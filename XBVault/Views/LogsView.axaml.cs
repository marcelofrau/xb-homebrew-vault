using System.Collections.Specialized;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class LogsView : UserControl
{
    private const int CopyFeedbackDelayMs = 2000;

    public LogsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is LogsViewModel vm)
        {
            vm.Logs.CollectionChanged += OnLogsChanged;
            ScrollToBottom();
        }
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is LogsViewModel vm && vm.AutoScroll)
        {
            Dispatcher.UIThread.Post(ScrollToBottom);
        }
    }

    private void ScrollToBottom()
    {
        LogScrollViewer?.ScrollToEnd();
    }

    private void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not LogsViewModel vm || vm.Logs.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var entry in vm.Logs)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(entry.ToString());
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            clipboard.SetTextAsync(sb.ToString()).FireAndForget();

        var orig = CopyButtonText.Text;
        CopyButtonText.Text = "Copied!";
        Task.Delay(CopyFeedbackDelayMs).FireAndForget();
        // restore text after delay without blocking UI thread
        Task.Run(async () =>
        {
            await Task.Delay(CopyFeedbackDelayMs).ConfigureAwait(false);
            CopyButtonText.Text = orig;
        }).FireAndForget();
    }

    private void OnClearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is LogsViewModel vm)
            vm.Logs.Clear();
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is LogsViewModel vm)
        {
            vm.CloseFilterCommand.Execute(null);
            e.Handled = true;
        }
    }
}
