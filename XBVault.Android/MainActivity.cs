using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace XBVault.Android;

[Activity(
    Label = "XBVault",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
