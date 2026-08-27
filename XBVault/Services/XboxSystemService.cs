#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XBVault.Services;

public class XboxSystemService : IXboxSystemService
{
    private readonly XboxAuthService _auth;

    public XboxSystemService(XboxAuthService auth)
    {
        _auth = auth;
    }

    internal TimeSpan ScreenshotRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    internal int ScreenshotMaxRetries { get; set; } = 5;

    public async Task<byte[]?> CaptureScreenshotAsync(CancellationToken ct = default)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("CaptureScreenshot called but not configured");
            return null;
        }

        for (int retry = 0; retry <= ScreenshotMaxRetries; retry++)
        {
            var attempt = retry + 1;
            try
            {
                var url = $"/ext/screenshot?download=true&hdr=false&time={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                if (attempt > 1)
                    Logger.Info($"GET {url} (attempt {attempt}/{ScreenshotMaxRetries})");
                else
                    Logger.Info($"GET {url}");
                var response = await _auth.Http.GetAsync(url, ct);
                Logger.Info($"GET screenshot => {(int)response.StatusCode}");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsByteArrayAsync(ct);

                var body = await _auth.ReadResponseBody(response);
                Logger.Warn($"Body: {body}");

                if (retry < ScreenshotMaxRetries)
                {
                    Logger.Info($"Screenshot returned {(int)response.StatusCode}, retrying in {ScreenshotRetryDelay.TotalMilliseconds}ms...");
                    await Task.Delay(ScreenshotRetryDelay, ct);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("CaptureScreenshot cancelled");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"CaptureScreenshot failed (attempt {attempt}/{ScreenshotMaxRetries})");
                if (retry < ScreenshotMaxRetries)
                    await Task.Delay(ScreenshotRetryDelay, ct);
            }
        }

        Logger.Warn($"Screenshot failed after {ScreenshotMaxRetries} attempts");
        return null;
    }

    public async Task<string?> GetSystemInfoAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetSystemInfo called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/systeminfo");
            var response = await _auth.Http.GetAsync("/api/systeminfo");
            Logger.Info($"GET /api/systeminfo => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/systeminfo failed: {await _auth.ReadResponseBody(response)}");

            Logger.Info("GET /api/os/info (fallback)");
            var fallback = await _auth.Http.GetAsync("/api/os/info");
            Logger.Info($"GET /api/os/info => {(int)fallback.StatusCode}");
            if (fallback.IsSuccessStatusCode)
                return await fallback.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/os/info also failed: {await _auth.ReadResponseBody(fallback)}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetSystemInfo failed");
            return null;
        }
    }

    public async Task<string?> GetCrashDumpsAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetCrashDumps called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/app/debug/crashdump");
            var response = await _auth.Http.GetAsync("/api/app/debug/crashdump");
            Logger.Info($"GET /api/app/debug/crashdump => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/app/debug/crashdump failed: {await _auth.ReadResponseBody(response)}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetCrashDumps failed");
            return null;
        }
    }

    public async Task<bool> DeleteCrashDumpAsync(string filename)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("DeleteCrashDump called but not configured");
            return false;
        }

        try
        {
            var encoded = Uri.EscapeDataString(filename);
            Logger.Info($"DELETE /api/app/debug/crashdump/{encoded}");
            var response = await _auth.DeleteWithCsrfAsync($"/api/app/debug/crashdump/{encoded}");
            Logger.Info($"DELETE crashdump => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"DeleteCrashDump failed for {filename}");
            return false;
        }
    }

    public async Task<string?> GetCrashControlAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetCrashControl called but not configured");
            return null;
        }

        try
        {
            Logger.Info("GET /api/app/debug/crashcontrol");
            var response = await _auth.Http.GetAsync("/api/app/debug/crashcontrol");
            Logger.Info($"GET /api/app/debug/crashcontrol => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            Logger.Warn($"GET /api/app/debug/crashcontrol failed: {await _auth.ReadResponseBody(response)}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetCrashControl failed");
            return null;
        }
    }

    public async Task<bool> SetCrashControlAsync(bool enabled)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("SetCrashControl called but not configured");
            return false;
        }

        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("CrashDumpEnabled", enabled ? "true" : "false")
            });
            Logger.Info($"POST /api/app/debug/crashcontrol (enabled={enabled})");
            var response = await _auth.PostWithCsrfAsync("/api/app/debug/crashcontrol", content);
            Logger.Info($"POST /api/app/debug/crashcontrol => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "SetCrashControl failed");
            return false;
        }
    }

    public async Task<bool> RestartXboxAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("RestartXbox called but not configured");
            return false;
        }

        try
        {
            Logger.Info("POST /api/control/restart");
            var response = await _auth.PostWithCsrfAsync("/api/control/restart", null);
            Logger.Info($"POST /api/control/restart => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "RestartXbox failed");
            return false;
        }
    }

    public async Task<bool> ShutdownXboxAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("ShutdownXbox called but not configured");
            return false;
        }

        try
        {
            Logger.Info("POST /api/control/shutdown");
            var response = await _auth.PostWithCsrfAsync("/api/control/shutdown", null);
            Logger.Info($"POST /api/control/shutdown => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ShutdownXbox failed");
            return false;
        }
    }
}
