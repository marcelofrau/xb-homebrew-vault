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
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ConnectionViewModel vm)
        {
            vm.OutputLines.CollectionChanged += OnOutputLinesChanged;
            vm.Completed += OnConnectionCompleted;
            vm.CloseAction = Close;
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConnectBtn?.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is ConnectionViewModel vm && vm.IsActive)
                vm.CancelCommand.Execute(null);
            Close();
            e.Handled = true;
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
        // Run close delay without crashing on exceptions from async void
        Task.Run(async () =>
        {
            try
            {
                Logger.Info($"Connection dialog completed: success={success}");
                if (success)
                    await Task.Delay(SuccessCloseDelayMs).ConfigureAwait(false);
                else
                    await Task.Delay(FailureCloseDelayMs).ConfigureAwait(false);
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnConnectionCompleted delayed close failed");
            }
        }).FireAndForget();
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
