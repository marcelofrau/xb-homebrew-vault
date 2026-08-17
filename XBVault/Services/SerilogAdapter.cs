using System;
using Serilog;

namespace XBVault.Services;

public sealed class SerilogAdapter : IAppLogger
{
    public void Trace(string msg) => Logger.Trace(msg);
    public void Debug(string msg) => Logger.Debug(msg);
    public void Info(string msg) => Logger.Info(msg);
    public void Warn(string msg) => Logger.Warn(msg);
    public void Error(string msg) => Logger.Error(msg);
    public void Error(Exception ex, string? context = null) => Logger.Error(ex, context);
    public void Fatal(string msg) => Logger.Fatal(msg);
    public void Fatal(Exception ex, string? context = null) => Logger.Fatal(ex, context);
}
