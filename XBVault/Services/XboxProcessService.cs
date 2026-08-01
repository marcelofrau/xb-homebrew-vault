using System;
using System.Threading.Tasks;

namespace XBVault.Services;

public class XboxProcessService
{
    private readonly XboxAuthService _auth;

    public XboxProcessService(XboxAuthService auth)
    {
        _auth = auth;
    }

    public async Task<string?> GetRunningTitleAsync()
    {
        if (!_auth.IsConfigured) return null;

        try
        {
            Logger.Info("GET /ext/app/runningtitle");
            var response = await _auth.Http.GetAsync("/ext/app/runningtitle");
            Logger.Info($"GET /ext/app/runningtitle => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var pfn = doc.RootElement.TryGetProperty("PackageFullName", out var p) ? p.GetString() : null;
            Logger.Info($"Running title: {(string.IsNullOrEmpty(pfn) ? "(none)" : pfn)}");
            return string.IsNullOrEmpty(pfn) ? null : pfn;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetRunningTitle failed");
            return null;
        }
    }

    public async Task<string?> GetProcessesAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetProcesses called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/resourcemanager/processes");
            var response = await _auth.Http.GetAsync("/api/resourcemanager/processes");
            Logger.Info($"GET /api/resourcemanager/processes => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetProcesses failed");
            return null;
        }
    }

    public async Task<bool> KillProcessAsync(int pid)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("KillProcess called but not configured");
            return false;
        }

        try
        {
            Logger.Info($"DELETE /api/resourcemanager/process?pid={pid}");
            var response = await _auth.Http.DeleteAsync($"/api/resourcemanager/process?pid={pid}");
            Logger.Info($"DELETE process => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "KillProcess failed");
            return false;
        }
    }
}
