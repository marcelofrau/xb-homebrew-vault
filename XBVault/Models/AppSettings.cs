using System.Text.Json.Serialization;

namespace XBVault.Models;

public class AppSettings
{
    public XboxConnection XboxConnection { get; set; } = new();
    public string LastSelectedTab { get; set; } = "Browse";
    public int CacheExpiryHours { get; set; } = 24;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public string MinLogLevel { get; set; } = "Info";
}
