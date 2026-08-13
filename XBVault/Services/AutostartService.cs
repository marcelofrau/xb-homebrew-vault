using System;
using XBVault.Models;

namespace XBVault.Services;

public class AutostartService
{
    public string? GetAutostartFullName()
    {
        try
        {
            return SettingsService.Current.AutostartPackageFullName;
        }
        catch (Exception ex)
        {
            Logger.Debug($"AutostartService: read failed — {ex.Message}");
            return null;
        }
    }

    public string? SetAutostart(string packageFullName)
    {
        var previous = GetAutostartFullName();
        SettingsService.Current.AutostartPackageFullName = packageFullName;
        SettingsService.Save();
        Logger.Info($"Autostart set to {packageFullName} (was {(previous ?? "none")})");
        return previous;
    }

    public void ClearAutostart()
    {
        SettingsService.Current.AutostartPackageFullName = null;
        SettingsService.Save();
        Logger.Info("Autostart cleared");
    }
}
