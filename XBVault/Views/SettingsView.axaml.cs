using Avalonia.Controls;
using XBVault.Helpers;

namespace XBVault.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        VersionText.Text = $"XB Homebrew Vault {BuildInfo.DisplayVersion}";
    }
}
