#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public class XboxPackageService : IXboxPackageService
{
    private const int PollDelayMs = 2000;
    private const int RetryDelayMs = 3000;

    private readonly XboxAuthService _auth;

    public XboxPackageService(XboxAuthService auth)
    {
        _auth = auth;
    }

    public async Task<List<InstalledPackage>> GetInstalledPackagesAsync()
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("GetInstalledPackages called but not configured");
            return [];
        }

        try
        {
            Logger.Info("GET /api/app/packagemanager/packages");
            var response = await _auth.Http.GetAsync("/api/app/packagemanager/packages");
            Logger.Info($"GET /api/app/packagemanager/packages => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
                response.EnsureSuccessStatusCode(); // will throw
            }

            var json = await response.Content.ReadAsStringAsync();
            Logger.Trace($"Packages JSON length: {json.Length} chars");

            using var doc = JsonDocument.Parse(json);
            var sample = doc.RootElement.TryGetProperty("InstalledPackages", out var arr) && arr.GetArrayLength() > 0
                ? arr[0].ToString() : "no packages";
            Logger.Info($"First package raw:\n{sample}");

            var result = JsonSerializer.Deserialize<PackagesResponse>(json);

            var count = result?.InstalledPackages?.Count ?? 0;
            Logger.Info($"Got {count} installed packages");

            if (result?.InstalledPackages is not null && arr.ValueKind == JsonValueKind.Array)
            {
                for (int i = 0; i < Math.Min(result.InstalledPackages.Count, arr.GetArrayLength()); i++)
                    result.InstalledPackages[i].RawJson = arr[i].ToString();
            }

            return result?.InstalledPackages ?? [];
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetInstalledPackages failed");
            return [];
        }
    }

    public async Task<bool> UninstallPackageAsync(string packageFullName)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("Uninstall called but not configured");
            return false;
        }

        try
        {
            Logger.Info($"Uninstalling: {packageFullName}");
            var encoded = Uri.EscapeDataString(packageFullName);
            var url = $"/api/app/packagemanager/package?package={encoded}";
            Logger.Info($"DELETE {url}");
            var response = await _auth.DeleteWithCsrfAsync(url);
            Logger.Info($"DELETE => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                Logger.Warn($"Body: {await _auth.ReadResponseBody(response)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Uninstall failed for {packageFullName}");
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> LaunchPackageAsync(string packageFullName, string packageRelativeId)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("Launch called but not configured");
            return (false, null);
        }

        try
        {
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(packageRelativeId));
            var encoded = Uri.EscapeDataString(b64);
            var url = $"/api/taskmanager/app?appid={encoded}";
            Logger.Info($"Launching: {packageRelativeId}");
            var response = await _auth.PostWithCsrfAsync(url, new StringContent(""));
            Logger.Info($"POST {url} => {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return (true, null);

            var body = await _auth.ReadResponseBody(response);
            Logger.Warn($"Body: {body}");
            var msg = XboxResponseParser.TryParseError(body) ?? $"HTTP {(int)response.StatusCode}";
            return (false, msg);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Launch failed for {packageRelativeId}");
            return (false, "Request failed");
        }
    }

    public async Task<HashSet<string>> GetRunningPackageNamesAsync()
    {
        if (!_auth.IsConfigured) return [];

        try
        {
            Logger.Info("GET /api/resourcemanager/processes (for running packages)");
            var response = await _auth.Http.GetAsync("/api/resourcemanager/processes");
            Logger.Info($"GET /api/resourcemanager/processes => {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<ProcessListResponse>(json);
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (parsed?.Processes is not null)
            {
                foreach (var p in parsed.Processes)
                {
                    if (!string.IsNullOrEmpty(p.PackageFullName))
                        running.Add(p.PackageFullName);
                    if (!string.IsNullOrEmpty(p.PackageFamilyName))
                        running.Add(p.PackageFamilyName);
                }
            }

            Logger.Info($"Processes with package info: {running.Count}");
            return running;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetRunningPackageNames failed");
            return [];
        }
    }

    public async Task<bool> SuspendPackageAsync(string packageFullName)
    {
        if (!_auth.IsConfigured) return false;
        try
        {
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(packageFullName));
            var encoded = Uri.EscapeDataString(b64);
            var url = $"/api/taskmanager/app/state?package={encoded}&state=suspend";
            Logger.Info($"Suspend: {packageFullName}");
            var response = await _auth.PostWithCsrfAsync(url, new StringContent(""));
            Logger.Info($"POST {url} => {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Suspend failed for {packageFullName}");
            return false;
        }
    }

    public async Task<bool> TerminatePackageAsync(string packageFullName)
    {
        if (!_auth.IsConfigured) return false;
        try
        {
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(packageFullName));
            var encoded = Uri.EscapeDataString(b64);
            var url = $"/api/taskmanager/app?package={encoded}";
            Logger.Info($"Terminate: {packageFullName}");
            var response = await _auth.DeleteWithCsrfAsync(url);
            Logger.Info($"DELETE {url} => {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Terminate failed for {packageFullName}");
            return false;
        }
    }

    private async Task TerminateRunningPackageAsync(string packagePath)
    {
        try
        {
            var pkgName = XboxResponseParser.ParseMsixPackageName(packagePath);
            if (string.IsNullOrEmpty(pkgName))
            {
                Logger.Debug("Could not parse package name from MSIX, skipping terminate check");
                return;
            }

            var runningNames = await GetRunningPackageNamesAsync();
            if (runningNames.Count == 0) return;

            var match = runningNames.FirstOrDefault(n =>
                n.StartsWith(pkgName + "_", StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                Logger.Info($"Running package matches '{pkgName}': {match}, terminating...");
                await TerminatePackageAsync(match);
                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to terminate running package: {ex.Message}");
        }
    }

    public async Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null)
    {
        var wrapped = progress is not null
            ? new Progress<InstallProgressInfo>(p => progress.Report(p.Total))
            : null;
        return await InstallPackageAsync(filePath, [], wrapped);
    }

    public async Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null)
    {
        if (!_auth.IsConfigured)
        {
            Logger.Warn("Install called but not configured");
            return false;
        }
        if (!File.Exists(packagePath))
        {
            Logger.Error($"Install file not found: {packagePath}");
            return false;
        }

        try
        {
            // Kill any running instance of this package before upload
            await TerminateRunningPackageAsync(packagePath);

            var totalFiles = 1 + dependencies.Length;
            var mainName = Path.GetFileName(packagePath);
            Logger.Info($"Install starting: {mainName} ({dependencies.Length} dependencies)");

            // Upload main package
            progress?.Report(new InstallProgressInfo
            {
                Total = 1.0 / totalFiles * 0,
                Status = $"Uploading {mainName}...",
                CurrentFile = mainName
            });

            var mainOk = await UploadAppxFile(packagePath, progress);
            if (!mainOk)
            {
                Logger.Error($"Main package upload failed: {mainName}");
                return false;
            }

            progress?.Report(new InstallProgressInfo
            {
                Total = 1.0 / totalFiles * 1,
                File = 1,
                Status = $"Uploaded main package",
                CurrentFile = mainName
            });

            // Upload dependencies one at a time
            Logger.Info($"Uploading {dependencies.Length} dependencies...");
            var depIndex = 0;
            foreach (var dep in dependencies)
            {
                depIndex++;
                if (!File.Exists(dep))
                {
                    Logger.Warn($"Dependency not found: {dep}");
                    continue;
                }

                var depName = Path.GetFileName(dep);
                Logger.Info($"  [{depIndex}/{dependencies.Length}] {depName}");
                progress?.Report(new InstallProgressInfo
                {
                    Total = (double)(1 + depIndex) / totalFiles,
                    Status = $"Uploading dependency {depIndex}/{dependencies.Length}: {depName}...",
                    CurrentFile = depName
                });

                await WaitForPackageManagerReady();

                var depOk = await UploadAppxFile(dep, progress);
                if (depOk)
                    Logger.Info($"  Dependency uploaded: {depName}");
                else
                    Logger.Error($"  Dependency failed: {depName}");
            }

            // Wait for final install to complete
            var installOk = await WaitForPackageManagerReady();
            if (!installOk)
            {
                Logger.Error("Install completed but package manager reported failure or timed out");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Install failed for {packagePath}");
            return false;
        }
    }

    private async Task<bool> UploadAppxFile(string filePath, IProgress<InstallProgressInfo>? progress = null)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = new FileInfo(filePath).Length;
        Logger.Info($"Uploading: {fileName} ({XboxResponseParser.SizeFormat(fileSize)})");

        for (int attempt = 0; attempt <= 5; attempt++)
        {
            if (attempt > 0)
            {
                var wait = attempt * 5;
                Logger.Info($"Waiting {wait}s (Xbox busy, attempt {attempt}/5)...");
                progress?.Report(new InstallProgressInfo
                {
                    Status = $"Waiting for previous install to finish ({wait}s)...",
                    CurrentFile = fileName
                });
                await Task.Delay(TimeSpan.FromSeconds(wait));
                await WaitForPackageManagerReady();
            }

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            // Build multipart body manually so Content-Type boundary is unquoted
            var boundary = "----XboxUploadBoundary";
            var headerBytes = Encoding.UTF8.GetBytes(
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\n" +
                $"Content-Type: application/octet-stream\r\n\r\n");
            var trailerBytes = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
            var bodyBytes = new byte[headerBytes.Length + fileBytes.Length + trailerBytes.Length];
            headerBytes.CopyTo(bodyBytes, 0);
            fileBytes.CopyTo(bodyBytes, headerBytes.Length);
            trailerBytes.CopyTo(bodyBytes, headerBytes.Length + fileBytes.Length);

            var content = new ByteArrayContent(bodyBytes);
            content.Headers.TryAddWithoutValidation("Content-Type",
                $"multipart/form-data; boundary={boundary}");

            var url = $"/api/app/packagemanager/package?package={Uri.EscapeDataString(fileName)}";
            Logger.Info($">> POST {url}");
            Logger.Info($"   Content-Type: {content.Headers.ContentType}");
            Logger.Info($"   Content-Length: {content.Headers.ContentLength ?? 0}");
            Logger.Info($"   File: {fileName} ({XboxResponseParser.SizeFormat(fileSize)})");

            progress?.Report(new InstallProgressInfo
            {
                Status = $"Uploading {fileName}...",
                CurrentFile = fileName,
                File = 0.3
            });

            var response = await _auth.PostWithCsrfAsync(url, content);

            Logger.Info($"<< {response.StatusCode:D} ({response.ReasonPhrase})");
            if (!response.IsSuccessStatusCode)
            {
                var body = await _auth.ReadResponseBody(response);
                Logger.Warn($"   Body: {body}");
            }
            else
            {
                var body = await _auth.ReadResponseBody(response);
                Logger.Info($"   Response: {body}");
            }

            progress?.Report(new InstallProgressInfo
            {
                Status = response.IsSuccessStatusCode
                    ? $"Uploaded {fileName} ✓"
                    : $"Upload failed: {fileName}",
                CurrentFile = fileName,
                File = response.IsSuccessStatusCode ? 1.0 : 0
            });

            if (response.StatusCode != System.Net.HttpStatusCode.Conflict)
                return response.IsSuccessStatusCode;
        }

        return false;
    }

    private async Task<bool> WaitForPackageManagerReady()
    {
        Logger.Info("Waiting for package manager to be ready...");
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await _auth.Http.GetAsync("/api/app/packagemanager/state");
                var code = (int)resp.StatusCode;
                Logger.Info($"GET /api/app/packagemanager/state => {code}");

                if (XboxResponseParser.IsIdleCode(resp.StatusCode))
                {
                    // 204/404 means no operation in progress
                    await Task.Delay(PollDelayMs);
                    var resp2 = await _auth.Http.GetAsync("/api/app/packagemanager/state");
                    var code2 = (int)resp2.StatusCode;
                    Logger.Info($"GET /api/app/packagemanager/state => {code2}");

                    if (XboxResponseParser.IsIdleCode(resp2.StatusCode))
                    {
                        Logger.Info("Package manager ready (got idle status twice)");
                        return true;
                    }

                    // Confirmation poll got 200+JSON — check Success
                    if (resp2.StatusCode == System.Net.HttpStatusCode.OK && XboxResponseParser.IsJsonSuccess(await resp2.Content.ReadAsStringAsync(), out var statusMsg))
                    {
                        Logger.Info($"Package manager ready (idle then success: {statusMsg})");
                        return true;
                    }

                    continue;
                }

                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    Logger.Debug($"GET /api/app/packagemanager/state body: {XboxResponseParser.Truncate(body, 500)}");

                    if (XboxResponseParser.IsJsonSuccess(body, out var statusMsg))
                    {
                        Logger.Info($"Package manager ready (operation completed: {statusMsg})");
                        return true;
                    }

                    if (XboxResponseParser.IsSignatureError(body))
                    {
                        Logger.Info("Package manager ready (TRUST_E_NOSIGNATURE — no operation in progress)");
                        return true;
                    }

                    if (XboxResponseParser.IsResourceInUseError(body, out var busyApps))
                    {
                        Logger.Error($"Package manager blocked — apps need to be closed: {busyApps}");
                        return false;
                    }

                    if (XboxResponseParser.IsHigherVersionError(body, out var higherVerMsg))
                    {
                        Logger.Warn($"Dependency skipped (higher version already installed): {higherVerMsg}");
                        return true;
                    }

                    if (XboxResponseParser.IsFatalDeploymentError(body, out var deployError))
                    {
                        Logger.Error($"Package manager deployment failed: {deployError}");
                        return false;
                    }

                    Logger.Warn($"Package manager state: {statusMsg} — not ready yet");
                    continue;
                }

                // Unexpected status code (4xx, 5xx, etc)
                Logger.Warn($"Package manager unexpected status: {code} {resp.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Package manager polling error: {ex.Message}");
            }
            await Task.Delay(RetryDelayMs);
        }
        Logger.Warn("Timed out waiting for package manager");
        return false;
    }
}
