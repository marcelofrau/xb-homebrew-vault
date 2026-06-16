using Avalonia.Controls;
using XBVault.Helpers;
using XBVault.Services;

namespace XBVault.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
        Opened += (_, _) => Logger.Debug("AboutWindow opened");
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Logger.Trace("AboutWindow closed");
        Close();
    }
}