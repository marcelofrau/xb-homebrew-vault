using Avalonia.Controls;
using Avalonia.Input;

namespace XBVault.Views;

public partial class NetworkInfoWindow : Window
{
    public NetworkInfoWindow()
    {
        InitializeComponent();
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
