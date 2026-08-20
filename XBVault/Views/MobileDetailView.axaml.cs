using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileDetailView : UserControl
{
    private Action? _onBack;

    public MobileDetailView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();

    private void OnFinishClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            vm.CloseDetailCommand.Execute(null);
        _onBack?.Invoke();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            vm.CloseDetailCommand.Execute(null);
        _onBack?.Invoke();
    }
}
