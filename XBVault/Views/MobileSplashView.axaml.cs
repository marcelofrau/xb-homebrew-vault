using Avalonia.Controls;
using XBVault.Helpers;

namespace XBVault.Views;

public partial class MobileSplashView : UserControl
{
    public MobileSplashView()
    {
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
    }
}
