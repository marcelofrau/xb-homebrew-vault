using Avalonia.Controls;
using XBVault.Helpers;

namespace XBVault;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
    }
}
