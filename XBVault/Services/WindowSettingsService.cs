using Avalonia;

namespace XBVault.Services;

public static class WindowSettingsService
{
    public const double DefaultMainWindowWidth = 1140;
    public const double DefaultMainWindowHeight = 860;
    public const double MinMainWindowWidth = 960;
    public const double MinMainWindowHeight = 720;
    public const double MaxMainWindowWidth = 3840;
    public const double MaxMainWindowHeight = 2160;

    public static Size GetMainWindowSize()
    {
        var settings = SettingsService.Current;
        return new Size(
            Normalize(settings.MainWindowWidth, DefaultMainWindowWidth, MinMainWindowWidth, MaxMainWindowWidth),
            Normalize(settings.MainWindowHeight, DefaultMainWindowHeight, MinMainWindowHeight, MaxMainWindowHeight));
    }

    public static void SaveMainWindowSize(double width, double height)
    {
        SettingsService.Current.MainWindowWidth = Normalize(width, DefaultMainWindowWidth, MinMainWindowWidth, MaxMainWindowWidth);
        SettingsService.Current.MainWindowHeight = Normalize(height, DefaultMainWindowHeight, MinMainWindowHeight, MaxMainWindowHeight);
        SettingsService.Save();
    }

    public static void ResetMainWindowSize()
    {
        SettingsService.Current.MainWindowWidth = 0;
        SettingsService.Current.MainWindowHeight = 0;
        SettingsService.Save();
    }

    private static double Normalize(double value, double fallback, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return fallback;

        return Math.Clamp(value, min, max);
    }
}
