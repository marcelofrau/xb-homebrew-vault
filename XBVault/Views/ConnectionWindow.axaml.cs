using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace XBVault.Views;

public partial class ConnectionWindow : Window
{
    public ConnectionWindow()
    {
        InitializeComponent();
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
