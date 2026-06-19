using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class LogsView : UserControl
{
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
}
