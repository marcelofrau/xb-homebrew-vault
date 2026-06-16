using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Collections.Specialized;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class RefreshWindow : Window
{
    public RefreshWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is RefreshViewModel vm)
        {
            vm.OutputLines.CollectionChanged += OnOutputLinesChanged;
            vm.Completed += OnRefreshCompleted;
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // Auto-start refresh after window opens
        if (DataContext is RefreshViewModel vm && vm.IsRunning is false)
        {
            vm.RefreshCommand.Execute(null);
        }
    }

    private void OnOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            OutputScroll?.ScrollToEnd();
        }
    }

    private void OnRefreshCompleted(bool success)
    {
        // Give user time to see final state, then close
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(1500);
            Close();
        });
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
