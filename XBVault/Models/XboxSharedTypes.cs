#nullable enable
namespace XBVault.Models;

public class PackagesResponse
{
    public List<InstalledPackage> InstalledPackages { get; set; } = [];
}

public record SshConnectionInfo(string Host, int Port, string Username, string Password);

public class ConnectionTestResult
{
    public bool Success { get; }
    public int? StatusCode { get; }
    public string? ErrorDetail { get; }
    public bool IsCancelled { get; }

    public ConnectionTestResult(bool success, int? statusCode, string? errorDetail, bool isCancelled = false)
    {
        Success = success;
        StatusCode = statusCode;
        ErrorDetail = errorDetail;
        IsCancelled = isCancelled;
    }
}
