using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace XBVault.Views;

public partial class InspectorView : UserControl
{
    public InspectorView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ViewModels.InspectorViewModel vm)
        {
            vm.ConsoleEntries.CollectionChanged += OnConsoleEntriesChanged;
        }
    }

    private void OnConsoleEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not ViewModels.InspectorViewModel vm || !vm.AutoScroll)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            ConsoleScrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is ViewModels.InspectorViewModel vm)
        {
            vm.CloseFilterCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnReplInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataContext is ViewModels.InspectorViewModel vm)
                vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
