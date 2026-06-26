using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Collections.Specialized;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class RefreshWindow : Window
{
    private const int CloseDelayMs = 1500;

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
        Logger.Debug("RefreshWindow opened — auto-starting refresh");
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
        Logger.Info($"Catalog refresh window completed: success={success}");
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(CloseDelayMs);
            Close();
        });
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace("RefreshWindow closed by user");
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
