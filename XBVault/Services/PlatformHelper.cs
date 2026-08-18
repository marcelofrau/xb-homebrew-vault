using System;

namespace XBVault.Services;

public static class PlatformHelper
{
    /// <summary>
    /// Platform-specific URL opener. Set by host project (Android or Desktop).
    /// </summary>
    public static Action<string>? OpenUrlAction { get; set; }

    public static void OpenUrl(string url)
    {
        if (OpenUrlAction is not null)
        {
            OpenUrlAction(url);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to open URL: {url}");
        }
    }
}
