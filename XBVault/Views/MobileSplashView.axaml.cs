using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Helpers;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileSplashView : UserControl
{
    public MobileSplashView()
    {
        Logger.Debug("MobileSplashView: constructor start");
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
        Logger.Debug($"MobileSplashView: version={BuildInfo.DisplayVersion}");
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InsetsManager == null) return;

        TopLevel.SetAutoSafeAreaPadding(this, false);
        var safe = topLevel.InsetsManager.SafeAreaPadding;
        if (safe.Bottom > 0)
        {
            Logger.Debug($"Android splash: SafeAreaPadding bottom={safe.Bottom}, adjusting progress bar");
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ProgressBar.Margin = new Thickness(32, 0, 32, 24 + safe.Bottom);
            });
        }
    }
}
