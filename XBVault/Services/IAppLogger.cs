using System;

namespace XBVault.Services;

public interface IAppLogger
{
    void Trace(string msg);
    void Debug(string msg);
    void Info(string msg);
    void Warn(string msg);
    void Error(string msg);
    void Error(Exception ex, string? context = null);
    void Fatal(string msg);
    void Fatal(Exception ex, string? context = null);
}
