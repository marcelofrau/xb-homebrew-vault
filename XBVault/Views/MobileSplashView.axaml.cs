using Avalonia.Controls;
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
    }
}
