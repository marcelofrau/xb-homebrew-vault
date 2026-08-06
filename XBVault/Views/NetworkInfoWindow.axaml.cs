using Avalonia.Controls;
using Avalonia.Input;
using XBVault.Helpers;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class NetworkInfoWindow : Window
{
    public NetworkInfoWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Opened += (_, _) => WindowFitHelper.ApplyScale(this, SettingsService.Current.UiScale);
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is NetworkInfoViewModel vm)
            await vm.RefreshCommand.ExecuteAsync(null);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
