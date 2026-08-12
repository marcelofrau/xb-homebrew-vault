using System.Text.Json.Serialization;

namespace XBVault.Models;

public class AppSettings
{
    public XboxConnection XboxConnection { get; set; } = new();
    public string LastSelectedTab { get; set; } = "Browse";
    public int CacheExpiryHours { get; set; } = 24;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public string MinLogLevel { get; set; } = "Info";
    public double LogFontSize { get; set; } = 15;
    public double ConsoleFontSize { get; set; } = 13;
    public double UiScale { get; set; } = 1.0;
    public bool AutoConnect { get; set; }
    public double MainWindowWidth { get; set; }
    public double MainWindowHeight { get; set; }
}
