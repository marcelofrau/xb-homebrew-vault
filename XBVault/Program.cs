using Avalonia;
using XBVault;
using XBVault.Helpers;
using XBVault.Services;

class Program
{
    public static PreFlightReport? PreFlightReport { get; private set; }

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            var consoleMode = false;

            foreach (var arg in args)
            {
                switch (arg.ToLowerInvariant())
                {
                    case "--help":
                    case "-h":
                    case "-?":
                        ShowHelp();
                        return;

                    case "--version":
                    case "-v":
                        ShowVersion();
                        return;

                    case "--console":
                    case "-c":
                        consoleMode = true;
                        break;

                    case "--reset-data":
                    case "-r":
                        HandleResetData();
                        return;

                    case "--check":
                        HandleCheck();
                        return;
                }
            }

            if (consoleMode)
                Logger.AttachConsole(allocNew: true);
            else
                Logger.AttachConsole();

            Logger.Info("Application starting");

            // Pre-flight: detect and auto-repair corruption
            PreFlightReport = PreFlightChecker.Run();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // If Avalonia boot fails, show native dialog (not silent death)
            try
            {
                PlatformDialog.Alert(
                    "XBVault - Fatal Error",
                    $"The application failed to start.\n\n{ex.GetType().Name}: {ex.Message}");
            }
            catch
            {
                // Last resort: write to stderr
                try { Console.Error.WriteLine($"FATAL: {ex}"); } catch { }
            }

            Environment.Exit(1);
        }
        finally
        {
            Logger.Info("Application exited");
            Logger.Shutdown();
        }
    }

    static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    static void ShowHelp()
    {
        Logger.AttachConsole();
        Console.WriteLine();
        Console.WriteLine("XB Homebrew Vault — Desktop manager for Xbox Dev Mode homebrew");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  XBVault [options]");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  --help, -h, -?       Show this help message");
        Console.WriteLine("  --version, -v        Show version information");
        Console.WriteLine("  --console, -c        Open a console window for log output (Windows)");
        Console.WriteLine("  --reset-data, -r     Reset all app data (settings, cache, logs)");
        Console.WriteLine("  --check              Run health checks and print report");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  XBVault                      Normal startup");
        Console.WriteLine("  XBVault --console            Start with visible console (debugging)");
        Console.WriteLine("  XBVault --reset-data         Reset all data and exit");
        Console.WriteLine("  XBVault --check              Run diagnostics");
        Console.WriteLine();
    }

    static void ShowVersion()
    {
        Logger.AttachConsole();
        Console.WriteLine($"XB Homebrew Vault {BuildInfo.DisplayVersion}");
    }

    static void HandleResetData()
    {
        Logger.AttachConsole();

        var title = "XB Homebrew Vault";
        var msg = "Reset all application data?\n\n" +
                  "This will delete:\n" +
                  "• Settings (connection config, credentials)\n" +
                  "• Package cache (downloaded files)\n" +
                  "• Log files\n\n" +
                  "The application will need to be restarted afterwards.";

        var confirmed = PlatformDialog.Confirm(title, msg);
        if (!confirmed)
        {
            Console.WriteLine("Reset cancelled.");
            return;
        }

        var errors = new List<string>();

        // Delete %APPDATA%/XBVault (settings + logs)
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XBVault");
        if (Directory.Exists(appData))
        {
            try
            {
                Directory.Delete(appData, true);
                Console.WriteLine("Deleted: " + appData);
            }
            catch (Exception ex)
            {
                errors.Add($"Settings/logs: {ex.Message}");
            }
        }

        // Delete %LOCALAPPDATA%/XBVault (cache + analysis)
        var localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XBVault");
        if (Directory.Exists(localAppData))
        {
            try
            {
                Directory.Delete(localAppData, true);
                Console.WriteLine("Deleted: " + localAppData);
            }
            catch (Exception ex)
            {
                errors.Add($"Cache: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            var errorMsg = "Some data could not be deleted:\n" +
                           string.Join("\n", errors.Select(e => $"• {e}")) +
                           "\n\nTry closing the app first, or manually delete the folders.";
            PlatformDialog.Alert(title, errorMsg);
        }
        else
        {
            PlatformDialog.Alert(title,
                "All application data has been reset.\n\n" +
                "Please restart the application.");
        }
    }

    static void HandleCheck()
    {
        Logger.AttachConsole();
        PreFlightChecker.RunHealthCheck();
    }
}
