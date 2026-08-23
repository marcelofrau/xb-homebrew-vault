using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileInstalledView : UserControl
{
    private Flyout? _openFlyout;

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
        Logger.Info("MobileInstalledView: Sideload button clicked");
        try
        {
            if (DataContext is InstalledViewModel vm)
            {
                Logger.Debug($"MobileInstalledView: DataContext OK, ShowCustomInstallAction is {(vm.ShowCustomInstallAction != null ? "wired" : "NULL")}");
                vm.OpenCustomInstallCommand.Execute(null);
            }
            else
            {
                Logger.Error($"MobileInstalledView: DataContext is not InstalledViewModel, it is {DataContext?.GetType().Name ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MobileInstalledView: Sideload click failed");
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        _openFlyout = sender as Flyout;
        Logger.Debug($"MobileInstalledView: Flyout opened, captured reference");
    }

    private void OnHamburgerItemClick(object? sender, RoutedEventArgs e)
    {
        Logger.Debug($"MobileInstalledView: HamburgerItemClick sender={sender?.GetType().Name}, flyout open={_openFlyout?.IsOpen}");
        if (_openFlyout is not null && _openFlyout.IsOpen)
        {
            var flyout = _openFlyout;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => flyout.IsOpen = false, Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}
