using Avalonia.Controls;

namespace XBVault.Views;

public partial class ItemDetailWindow : Window
{
    public ItemDetailWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
