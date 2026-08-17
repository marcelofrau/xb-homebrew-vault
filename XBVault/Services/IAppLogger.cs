#nullable enable
using System;

namespace XBVault.Services;

/// <summary>
/// Minimal logger abstraction for services that need testable logging.
/// </summary>
/// <remarks>
/// UI-facing logging is still handled by <see cref="Logger"/>. Use this interface when injecting a fake logger
/// into services under test or when adding platform-specific log sinks.
/// </remarks>
public interface IAppLogger
{
    /// <summary>Logs a trace message.</summary>
    void Trace(string msg);

    /// <summary>Logs a debug message.</summary>
    void Debug(string msg);

    /// <summary>Logs an informational message.</summary>
    void Info(string msg);

    /// <summary>Logs a warning message.</summary>
    void Warn(string msg);

    /// <summary>Logs an error message.</summary>
    void LogError(string msg);

    /// <summary>Logs an exception with optional contextual text.</summary>
    void LogError(Exception ex, string? context = null);

    /// <summary>Logs a fatal message.</summary>
    void Fatal(string msg);

    /// <summary>Logs a fatal exception with optional contextual text.</summary>
    void Fatal(Exception ex, string? context = null);
}
