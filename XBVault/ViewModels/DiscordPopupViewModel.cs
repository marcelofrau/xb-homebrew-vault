using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.ViewModels;

public partial class DiscordPopupViewModel : ObservableObject
{
    private readonly string _revivesUrl = "https://discord.gg/cBYsQCS7j7";
    private readonly string _xboxHubUrl = "https://discord.gg/pVd47KAG24";
    private readonly string _erUrl = "https://discord.gg/j2HndpJTej";

    [RelayCommand]
    private void JoinRevives()
    {
        OpenUrl(_revivesUrl);
    }

    [RelayCommand]
    private void JoinXboxHub()
    {
        OpenUrl(_xboxHubUrl);
    }

    [RelayCommand]
    private void JoinEr()
    {
        OpenUrl(_erUrl);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
