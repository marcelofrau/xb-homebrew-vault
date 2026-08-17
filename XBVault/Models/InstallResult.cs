#nullable enable
namespace XBVault.Models;

public enum InstallFailureStage
{
    None,
    Download,
    Extraction,
    Install
}

public sealed class InstallResult
{
    public bool Success { get; init; }
    public InstallFailureStage Stage { get; init; }
    public string? Message { get; init; }

    public static InstallResult Ok() => new() { Success = true };
    public static InstallResult Fail(InstallFailureStage stage, string message) => new()
    {
        Success = false,
        Stage = stage,
        Message = message
    };
}
