using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Models;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileBrowseView : UserControl
{
    public MobileBrowseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        XBVault.Services.Logger.Info("Android: MobileBrowseView loaded");
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm && sender is ListBox listBox && listBox.SelectedItem is CatalogItem item)
        {
            XBVault.Services.Logger.Info($"Android: Browse item tapped: {item.Name}");
            vm.SelectItemCommand.Execute(item);
            listBox.SelectedItem = null;
        }
    }
}
