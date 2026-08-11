using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using XBVault.Helpers;

namespace XBVault.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        VersionText.Text = $"XB Homebrew Vault {BuildInfo.DisplayVersion}";
        IntervalNud.AddHandler(InputElement.KeyDownEvent, OnIntervalNudKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnIntervalNudKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down))
            return;
        var nud = (NumericUpDown)sender!;
        var delta = e.Key == Key.Up ? nud.Increment : -nud.Increment;
        nud.Value = Math.Clamp((nud.Value ?? 0) + delta, nud.Minimum, nud.Maximum);
        e.Handled = true;
    }
}
