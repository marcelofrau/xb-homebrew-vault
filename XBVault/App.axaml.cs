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
            var cacheService = new CacheService();
            var installService = new PackageInstallService(cacheService, xboxService);

            var mainViewModel = new MainViewModel(xboxService);
            var browseViewModel = new BrowseViewModel(erService, installService);
            var installedViewModel = new InstalledViewModel(xboxService);
            var settingsViewModel = new SettingsViewModel(xboxService, cacheService);

            var splash = new SplashWindow();
            splash.Show();

            var main = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainViewModel.ShowAboutAction = () =>
            {
                var about = new Views.AboutWindow();
                about.ShowDialog(main);
            };

            var browseView = new Views.BrowseView { DataContext = browseViewModel };
            var installedView = new Views.InstalledView { DataContext = installedViewModel };
            var settingsView = new Views.SettingsView { DataContext = settingsViewModel };

            main.ViewCarousel.Items.Add(browseView);
            main.ViewCarousel.Items.Add(installedView);
            main.ViewCarousel.Items.Add(settingsView);

            splash.Closed += (_, _) =>
            {
                main.Show();
                _ = browseViewModel.LoadCatalogCommand.ExecuteAsync(null);
                _ = installedViewModel.RefreshPackagesCommand.ExecuteAsync(null);
            };

            splash.CloseAfterDelay();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
