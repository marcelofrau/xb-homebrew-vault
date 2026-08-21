using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileInstalledView : UserControl
{
    public MobileInstalledView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstalledViewModel vm)
        {
            vm.StartPolling();
            if (vm.Packages.Count == 0 && vm.IsConnected)
                vm.RefreshPackagesCommand.Execute(null);
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is InstalledViewModel vm && sender is ListBox listBox && listBox.SelectedItem is Models.InstalledPackage pkg)
        {
            vm.SelectedPackage = pkg;
            listBox.SelectedItem = null;
        }
    }

    private void OnSideloadClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstalledViewModel vm)
            vm.OpenCustomInstallCommand.Execute(null);
    }
}
