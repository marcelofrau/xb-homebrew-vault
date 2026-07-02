using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.ViewModels;

public partial class DiscordPopupViewModel : ObservableObject
{
    [RelayCommand]
    private void JoinRevives()
    {
        OpenUrl("https://discord.gg/cBYsQCS7j7");
    }

    [RelayCommand]
    private void JoinXboxHub()
    {
        OpenUrl("https://discord.gg/pVd47KAG24");
    }

    [RelayCommand]
    private void JoinEr()
    {
        OpenUrl("https://discord.gg/j2HndpJTej");
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
