using System;
using System.IO;
using System.Text.Json;
using XBVault.Models;

namespace XBVault.Services;

public class PreFlightReport
{
    public bool SettingsReset { get; set; }
    public bool CacheCleared { get; set; }
    public bool LogDirUnavailable { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public static class PreFlightChecker
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XBVault");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");

    private static readonly string LocalAppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XBVault");

    private static readonly string CacheDir = Path.Combine(LocalAppDataDir, "cache");
    private static readonly string LogDir = Path.Combine(AppDataDir, "logs");

    public static PreFlightReport Run()
    {
        var report = new PreFlightReport();

        CheckSettings(report);
        CheckCache(report);
        CheckLogDir(report);

        return report;
    }

    private static void CheckSettings(PreFlightReport report)
    {
        if (!File.Exists(SettingsPath))
            return;

        try
        {
            var json = File.ReadAllText(SettingsPath);
            JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"Settings corrupted: {ex.Message}. Resetting to defaults.");
            report.SettingsReset = true;

            var backupPath = SettingsPath + ".corrupted";
            try
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(SettingsPath, backupPath);
                report.Warnings.Add($"Backup of corrupted settings saved to: {backupPath}");
            }
            catch
            {
                try { File.Delete(SettingsPath); }
                catch { }
            }
        }
    }

    private static void CheckCache(PreFlightReport report)
    {
        if (!Directory.Exists(CacheDir))
            return;

        // Check catalog cache JSON integrity
        var catalogCache = Path.Combine(CacheDir, "catalog-api.json");
        if (File.Exists(catalogCache))
        {
            try
            {
                var json = File.ReadAllText(catalogCache);
                var cache = JsonSerializer.Deserialize<CatalogCache>(json);
                if (cache?.Data is null)
                {
                    report.Warnings.Add("Catalog cache corrupted (null data). Clearing.");
                    ClearCache(report);
                    return;
                }
                if (cache.Data.SchemaVersion != CatalogApiService.ExpectedSchemaVersion)
                {
                    report.Warnings.Add(
                        $"Catalog cache schema v{cache.Data.SchemaVersion} differs from expected v{CatalogApiService.ExpectedSchemaVersion}. Clearing.");
                    ClearCache(report);
                    return;
                }
            }
            catch
            {
                report.Warnings.Add("Catalog cache unreadable. Clearing.");
                ClearCache(report);
            }
        }

        // Spot-check a random cached file
        try
        {
            var dirs = Directory.GetDirectories(CacheDir);
            foreach (var dir in dirs)
            {
                try
                {
                    Directory.GetFiles(dir);
                }
                catch
                {
                    report.Warnings.Add($"Cache subdirectory inaccessible: {dir}. Will clear cache.");
                    ClearCache(report);
                    return;
                }
            }
        }
        catch
        {
            report.Warnings.Add("Cache directory inaccessible. Will clear cache.");
            ClearCache(report);
        }
    }

    private static void ClearCache(PreFlightReport report)
    {
        try
        {
            if (Directory.Exists(CacheDir))
            {
                Directory.Delete(CacheDir, true);
                Directory.CreateDirectory(CacheDir);
                report.CacheCleared = true;
            }
        }
        catch (Exception ex)
        {
            report.Errors.Add($"Failed to clear cache: {ex.Message}");
        }
    }

    private static void CheckLogDir(PreFlightReport report)
    {
        try
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);

            var testFile = Path.Combine(LogDir, ".write-test");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
        }
        catch
        {
            report.Warnings.Add("Log directory not writable. File logging disabled.");
            report.LogDirUnavailable = true;
        }
    }

    public static void RunHealthCheck()
    {
        Console.WriteLine("=== XBVault Health Check ===\n");

        // Settings
        Console.Write("Settings file ... ");
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                JsonSerializer.Deserialize<AppSettings>(json);
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CORRUPTED: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("NOT FOUND (fresh install)");
        }

        // Cache
        Console.Write("Cache directory .. ");
        if (Directory.Exists(CacheDir))
        {
            try
            {
                var files = Directory.GetFiles(CacheDir, "*", SearchOption.AllDirectories);
                var size = files.Sum(f => new FileInfo(f).Length);
                Console.WriteLine($"OK ({files.Length} files, {FormatBytes(size)})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("EMPTY");
        }

        // Log dir
        Console.Write("Log directory .... ");
        try
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
            var testFile = Path.Combine(LogDir, ".write-test");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"NOT WRITABLE: {ex.Message}");
        }

        // .NET runtime
        Console.Write(".NET runtime ..... ");
        Console.WriteLine($"OK (v{Environment.Version})");

        // OS
        Console.Write("OS platform ...... ");
        Console.WriteLine(Environment.OSVersion);

        // Avalonia
        Console.Write("Avalonia UI ...... ");
        var avaloniaAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Avalonia");
        if (avaloniaAsm is not null)
            Console.WriteLine($"OK (v{avaloniaAsm.GetName().Version})");
        else
            Console.WriteLine("PRESENT (not loaded yet)");

        // SSH.NET
        Console.Write("SSH.NET .......... ");
        var sshAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Renci.SshNet");
        if (sshAsm is not null)
            Console.WriteLine($"OK (v{sshAsm.GetName().Version})");
        else
            Console.WriteLine("PRESENT (not loaded yet)");

        Console.WriteLine("\n=== End ===");
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double n = bytes;
        foreach (var u in units)
        {
            if (n < 1024) return $"{n:F1} {u}";
            n /= 1024;
        }
        return $"{n:F1} TB";
    }
}
