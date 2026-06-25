using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace XBVault.Services;

public enum LogLevel { Trace, Debug, Info, Warn, Error, Fatal }

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;

    public string Marker => Level switch
    {
        LogLevel.Trace => "\U0001f50d ",  // 🔍
        LogLevel.Debug => "\U0001f41b ",  // 🐛
        LogLevel.Info  => "\u2713 ",      // ✓
        LogLevel.Warn  => "\u26a0 ",      // ⚠
        LogLevel.Error => "\u2716 ",      // ✖
        LogLevel.Fatal => "\U0001f480 ",  // 💀
        _              => "? "
    };

    public string Color => Level switch
    {
        LogLevel.Trace => "#8B8D91",
        LogLevel.Debug => "#5A5C60",
        LogLevel.Info  => "#2ECC71",
        LogLevel.Warn  => "#F39C12",
        LogLevel.Error => "#E74C3C",
        LogLevel.Fatal => "#E74C3C",
        _              => "#F0F0F0"
    };

    public override string ToString()
    {
        var lvl = Level switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Info  => "INFO",
            LogLevel.Warn  => "WARN",
            LogLevel.Error => "ERR ",
            LogLevel.Fatal => "FATL",
            _              => "????"
        };
        return $"[{Timestamp:HH:mm:ss.fff}] [{lvl}] {Message}";
    }
}

public static class Logger
{
    private static readonly object _lock = new();
    private static bool _consoleAttached;
    private static LogLevel _minLevel = LogLevel.Info;
    private static StreamWriter? _fileWriter;
    private static string? _logDir;

    public static LogLevel MinLevel
    {
        get => _minLevel;
        set => _minLevel = value;
    }

    // in-memory log lines for UI
    public static ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();
    public static event Action<LogEntry>? OnLog;

    /// <summary>
    /// Initialize file logging to %APPDATA%/XBVault/logs/ with rotation.
    /// Keeps last 5 log files, deletes oldest.
    /// </summary>
    public static void Init()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XBVault", "logs");

        try
        {
            Directory.CreateDirectory(_logDir);

            // Rotate: keep 5 newest, delete rest
            var existing = Directory.GetFiles(_logDir, "XBVault-*.log")
                .OrderByDescending(f => f)
                .ToList();
            foreach (var old in existing.Skip(4))
                File.Delete(old);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
            var logPath = Path.Combine(_logDir, $"XBVault-{timestamp}.log");
            _fileWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
            Info($"Log file: {logPath}");
        }
        catch (Exception ex)
        {
            // File logging unavailable — continue without it
            try { System.Diagnostics.Debug.WriteLine($"Logger.Init failed: {ex.Message}"); } catch { }
        }
    }

    public static void AttachConsole()
    {
        if (_consoleAttached) return;

        try
        {
            if (OperatingSystem.IsWindows())
                NativeMethods.AttachConsole(-1);

            _ = Console.BufferWidth;
            _consoleAttached = true;
        }
        catch
        {
            _consoleAttached = false;
        }

        var envLevel = Environment.GetEnvironmentVariable("XBVAULT_LOG_LEVEL")?.ToUpperInvariant();
        if (envLevel is not null)
        {
            _minLevel = envLevel switch
            {
                "TRACE" => LogLevel.Trace,
                "DEBUG" => LogLevel.Debug,
                "INFO"  => LogLevel.Info,
                "WARN"  => LogLevel.Warn,
                "ERROR" => LogLevel.Error,
                "FATAL" => LogLevel.Fatal,
                _       => _minLevel
            };
        }
    }

    private static void WriteConsole(LogEntry e)
    {
        var orig = Console.ForegroundColor;
        Console.ForegroundColor = e.Level switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info  => ConsoleColor.Green,
            LogLevel.Warn  => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Fatal => ConsoleColor.DarkRed,
            _              => ConsoleColor.White
        };
        Console.WriteLine(e.ToString());
        Console.ForegroundColor = orig;
    }

    private static void Push(LogLevel level, string message)
    {
        if (level < _minLevel) return;

        var entry = new LogEntry { Level = level, Message = message, Timestamp = DateTime.Now };
        lock (_lock)
        {
            try { Entries.Add(entry); } catch { }
            try { OnLog?.Invoke(entry); } catch { }
            try { WriteConsole(entry); } catch { }
            if (_fileWriter is not null)
            {
                try { _fileWriter.WriteLine(entry.ToString()); } catch { }
            }
        }
    }

    public static void Trace(string msg) => Push(LogLevel.Trace, msg);
    public static void Debug(string msg) => Push(LogLevel.Debug, msg);
    public static void Info(string msg) => Push(LogLevel.Info, msg);
    public static void Warn(string msg) => Push(LogLevel.Warn, msg);
    public static void Error(string msg) => Push(LogLevel.Error, msg);
    public static void Error(Exception ex, string? context = null)
    {
        var msg = context is null ? ex.ToString() : $"{context}: {ex}";
        Push(LogLevel.Error, msg);
    }
    public static void Fatal(string msg) => Push(LogLevel.Fatal, msg);
    public static void Fatal(Exception ex, string? context = null)
    {
        var msg = context is null ? ex.ToString() : $"{context}: {ex}";
        Push(LogLevel.Fatal, msg);
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(int dwProcessId);
    }
}
