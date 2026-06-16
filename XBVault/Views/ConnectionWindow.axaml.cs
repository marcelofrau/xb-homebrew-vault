using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Specialized;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class ConnectionWindow : Window
{
    public ConnectionWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) => Logger.Debug("ConnectionWindow opened");
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

    private void OnConnectionCompleted(bool success)
    {
        Logger.Info($"Connection dialog completed: success={success}");
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
