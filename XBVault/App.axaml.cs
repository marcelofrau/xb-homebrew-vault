using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using XBVault.Services;
using XBVault.ViewModels;
using XBVault.Views;

namespace XBVault;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var xboxService = new XboxDeviceService();
            var erService = new EmulationRevivalService();

            var mainViewModel = new MainViewModel(xboxService);
            var browseViewModel = new BrowseViewModel(erService);
            var settingsViewModel = new SettingsViewModel(xboxService);

            var splash = new SplashWindow();
            splash.Show();

            var main = new MainWindow
            {
                DataContext = mainViewModel
            };

            main.BrowseViewCtrl.DataContext = browseViewModel;
            main.SettingsViewCtrl.DataContext = settingsViewModel;

            splash.Closed += (_, _) =>
            {
                main.Show();
                _ = browseViewModel.LoadCatalogCommand.ExecuteAsync(null);
            };

            splash.CloseAfterDelay();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
