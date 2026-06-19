namespace XBVault.Models;

public class InstallProgressInfo
{
    public double Total { get; set; }
    public double File { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentFile { get; set; } = string.Empty;
}
