using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XBVault.Helpers;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileConnectionView : UserControl
{
    private Action? _onBack;

    public MobileConnectionView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
        DataContextChanged += OnDataContextChanged;
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ConnectionViewModel vm)
        {
            vm.OutputLines.CollectionChanged += OnOutputLinesChanged;
            vm.Completed += OnConnectionCompleted;
            vm.CloseAction = () => _onBack?.Invoke();
        }
    }

    private void OnConnectionCompleted(bool success)
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(success ? 2000 : 1500).ConfigureAwait(false);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _onBack?.Invoke());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "MobileConnectionView: delayed close failed");
            }
        }).FireAndForget();
    }

    private void OnOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            OutputScroll?.ScrollToEnd();
    }
}
