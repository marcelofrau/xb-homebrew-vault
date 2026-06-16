using System;
using System.Collections.ObjectModel;

namespace XBVault.Services;

public enum LogLevel { Debug, Info, Warn, Error }

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public override string ToString() => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Level.ToString().ToUpper(),-5} {Message}";
}

public static class Logger
{
    // in-memory log lines for UI
    public static ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();

    public static event Action<LogEntry>? OnLog;

    private static void WriteConsole(LogEntry e)
    {
        var orig = Console.ForegroundColor;
        Console.ForegroundColor = e.Level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.Green,
            LogLevel.Warn => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
        Console.WriteLine(e.ToString());
        Console.ForegroundColor = orig;
    }

    private static void Push(LogLevel level, string message)
    {
        var entry = new LogEntry { Level = level, Message = message, Timestamp = DateTime.Now };
        try
        {
            Entries.Add(entry);
            OnLog?.Invoke(entry);
        }
        catch { }
        try { WriteConsole(entry); } catch { }
    }

    public static void Debug(string msg) => Push(LogLevel.Debug, msg);
    public static void Info(string msg) => Push(LogLevel.Info, msg);
    public static void Warn(string msg) => Push(LogLevel.Warn, msg);
    public static void Error(string msg) => Push(LogLevel.Error, msg);
}
