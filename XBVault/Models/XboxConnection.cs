namespace XBVault.Models;

public class XboxConnection
{
    public string Address { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public bool UseHttps { get; set; } = true;
    public int Port { get; set; } = 11443;

    public string BaseUrl => $"{(UseHttps ? "https" : "http")}://{Address}:{Port}";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Address) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(EncryptedPassword);
}
