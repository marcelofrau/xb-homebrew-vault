using Avalonia;
using XBVault;
using XBVault.Services;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Logger.AttachConsole();
        Logger.Info("Application starting");

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex, "Unhandled exception in Main");
            throw;
        }
        finally
        {
            Logger.Info("Application exited");
        }
    }

    static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
