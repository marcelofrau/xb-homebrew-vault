using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
        }
    }

    private void OnOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            OutputScroll?.ScrollToEnd();
    }
}
