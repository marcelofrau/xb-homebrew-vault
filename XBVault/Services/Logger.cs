using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

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
        LogLevel.Trace => "#8AE234",
        LogLevel.Debug => "#729FCF",
        LogLevel.Info  => "#EEEEEC",
        LogLevel.Warn  => "#FCE94F",
        LogLevel.Error => "#EF2929",
        LogLevel.Fatal => "#F57900",
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
    private const int MaxEntries = 5_000;
    private const int MaxRetainedLogFiles = 5;

    private static readonly object _lock = new();
    private static bool _consoleAttached;
    private static LogLevel _minLevel = LogLevel.Info;
    private static readonly LoggingLevelSwitch _levelSwitch = new(ToSerilogLevel(LogLevel.Info));
    private static string? _logDir;
    private static ILogger _logger = Serilog.Core.Logger.None;
    private static bool _initialized;

    public static LogLevel MinLevel
    {
        get => _minLevel;
        set
        {
            _minLevel = value;
            _levelSwitch.MinimumLevel = ToSerilogLevel(value);
        }
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
            CleanupOldLogs();

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
            var logPath = Path.Combine(_logDir, $"XBVault-{timestamp}.log");
            const string outputTemplate = "[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] {Message:lj}{NewLine}{Exception}";

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
                .WriteTo.Sink(new ApplicationLogSink())
                .WriteTo.File(logPath, outputTemplate: outputTemplate, shared: false);

            if (_consoleAttached)
            {
                loggerConfiguration = loggerConfiguration.WriteTo.Console(
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate: outputTemplate);
            }

            _logger = loggerConfiguration.CreateLogger();
            Log.Logger = _logger;
            _initialized = true;
            Info($"Log file: {logPath}");
        }
        catch (Exception ex)
        {
            // File logging unavailable: keep UI/console logging alive.
            ConfigureWithoutFile();
            try { System.Diagnostics.Debug.WriteLine($"Logger.Init failed: {ex.Message}"); } catch { }
        }
    }

    private static void ConfigureWithoutFile()
    {
        const string outputTemplate = "[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] {Message:lj}{NewLine}{Exception}";

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.Sink(new ApplicationLogSink());

        if (_consoleAttached)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                outputTemplate: outputTemplate);
        }

        _logger = loggerConfiguration.CreateLogger();
        Log.Logger = _logger;
        _initialized = true;
    }

    public static void Shutdown()
    {
        try { Log.CloseAndFlush(); } catch { }
    }

    public static void AttachConsole(bool allocNew = false)
    {
        if (_consoleAttached) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (allocNew)
                    NativeMethods.AllocConsole();
                else
                    NativeMethods.AttachConsole(-1);
            }

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
            _levelSwitch.MinimumLevel = ToSerilogLevel(_minLevel);
        }
    }

    private static void WriteConsoleFallback(LogEntry e)
    {
        if (!_consoleAttached) return;

        var orig = Console.ForegroundColor;
        Console.ForegroundColor = e.Level switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.Green,
            LogLevel.Warn => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Fatal => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };
        Console.WriteLine(e.ToString());
        Console.ForegroundColor = orig;
    }

    private static void Push(LogLevel level, string message)
    {
        if (level < _minLevel) return;

        if (!_initialized)
        {
            var entry = new LogEntry { Level = level, Message = message, Timestamp = DateTime.Now };
            try { AddEntry(entry); } catch { }
            try { WriteConsoleFallback(entry); } catch { }
            return;
        }

        try { _logger.Write(ToSerilogLevel(level), "{LogMessage:l}", message); } catch { }
    }

    private static void Push(LogLevel level, Exception ex, string? context)
    {
        if (level < _minLevel) return;

        var message = context ?? ex.Message;
        if (!_initialized)
        {
            var entry = new LogEntry { Level = level, Message = context is null ? ex.ToString() : $"{context}: {ex}", Timestamp = DateTime.Now };
            try { AddEntry(entry); } catch { }
            try { WriteConsoleFallback(entry); } catch { }
            return;
        }

        try { _logger.Write(ToSerilogLevel(level), ex, "{LogMessage:l}", message); } catch { }
    }

    public static void Trace(string msg) => Push(LogLevel.Trace, msg);
    public static void Debug(string msg) => Push(LogLevel.Debug, msg);
    public static void Info(string msg) => Push(LogLevel.Info, msg);
    public static void Warn(string msg) => Push(LogLevel.Warn, msg);
    public static void Error(string msg) => Push(LogLevel.Error, msg);
    public static void Error(Exception ex, string? context = null)
    {
        Push(LogLevel.Error, ex, context);
    }
    public static void Fatal(string msg) => Push(LogLevel.Fatal, msg);
    public static void Fatal(Exception ex, string? context = null)
    {
        Push(LogLevel.Fatal, ex, context);
    }

    private static void CleanupOldLogs()
    {
        if (_logDir is null) return;

        var existing = Directory.GetFiles(_logDir, "XBVault-*.log")
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Skip(MaxRetainedLogFiles - 1);

        foreach (var old in existing)
        {
            try { File.Delete(old); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void AddEntry(LogEntry entry)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                AddEntryCore(entry);
                return;
            }

            Dispatcher.UIThread.Post(() => AddEntryCore(entry));
        }
        catch
        {
            AddEntryCore(entry);
        }
    }

    private static void AddEntryCore(LogEntry entry)
    {
        lock (_lock)
        {
            try
            {
                Entries.Add(entry);
                while (Entries.Count > MaxEntries)
                    Entries.RemoveAt(0);
                OnLog?.Invoke(entry);
            }
            catch { }
        }
    }

    private static LogEventLevel ToSerilogLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Info => LogEventLevel.Information,
        LogLevel.Warn => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Fatal => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };

    private static LogLevel FromSerilogLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Trace,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Info,
        LogEventLevel.Warning => LogLevel.Warn,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Fatal,
        _ => LogLevel.Info
    };

    private sealed class ApplicationLogSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            AddEntry(new LogEntry
            {
                Timestamp = logEvent.Timestamp.LocalDateTime,
                Level = FromSerilogLevel(logEvent.Level),
                Message = logEvent.RenderMessage() + (logEvent.Exception is null ? string.Empty : Environment.NewLine + logEvent.Exception)
            });
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AllocConsole();
    }
}
