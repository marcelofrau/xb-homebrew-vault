using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Specialized;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class ConnectionWindow : Window
{
    private const int SuccessCloseDelayMs = 2000;
    private const int FailureCloseDelayMs = 1500;

    public ConnectionWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) => Logger.Debug("ConnectionWindow opened");
        Closing += OnClosing;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ConnectionViewModel vm)
        {
            vm.OutputLines.CollectionChanged += OnOutputLinesChanged;
            vm.Completed += OnConnectionCompleted;
        }
    }

    private void OnOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            OutputScroll?.ScrollToEnd();
        }
    }

    private async void OnConnectionCompleted(bool success)
    {
        Logger.Info($"Connection dialog completed: success={success}");
        if (success)
            await Task.Delay(SuccessCloseDelayMs);
        else
            await Task.Delay(FailureCloseDelayMs);
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is ConnectionViewModel vm && vm.IsRunning)
        {
            e.Cancel = true;
            vm.CancelCommand.Execute(null);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace("ConnectionWindow closed by user");
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
