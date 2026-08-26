#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
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

    public async Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null, CancellationToken cancellationToken = default)
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

            var mainOk = await UploadAppxFile(packagePath, progress, cancellationToken);
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
                cancellationToken.ThrowIfCancellationRequested();
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

                await WaitForPackageManagerReady(cancellationToken);

                var depOk = await UploadAppxFile(dep, progress, cancellationToken);
                if (depOk)
                    Logger.Info($"  Dependency uploaded: {depName}");
                else
                    Logger.Error($"  Dependency failed: {depName}");
            }

            // Wait for final install to complete
            var installOk = await WaitForPackageManagerReady(cancellationToken);
            if (!installOk)
            {
                // The Xbox may have accepted the deploy (202) but timed out due to
                // 0x80073D02 (app in use). The console can still complete the install
                // asynchronously when the blocking app closes. Verify before reporting failure.
                var pkgName = XboxResponseParser.ParseMsixPackageName(packagePath);
                if (!string.IsNullOrEmpty(pkgName))
                {
                    Logger.Info("Package manager timed out — waiting 15s then verifying if install completed...");
                    await Task.Delay(15000, cancellationToken);

                    try
                    {
                        var installed = await GetInstalledPackagesAsync();
                        var found = installed.FirstOrDefault(p =>
                            p.FullName?.StartsWith(pkgName + "_", StringComparison.OrdinalIgnoreCase) == true);

                        if (found is not null)
                        {
                            Logger.Info($"Post-timeout verification: package found — {found.FullName} (install succeeded despite timeout)");
                            return true;
                        }
                    }
                    catch (Exception verifyEx)
                    {
                        Logger.Warn($"Post-timeout verification failed: {verifyEx.Message}");
                    }
                }

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

    private async Task<bool> UploadAppxFile(string filePath, IProgress<InstallProgressInfo>? progress = null, CancellationToken cancellationToken = default)
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
                await Task.Delay(TimeSpan.FromSeconds(wait), cancellationToken);
                await WaitForPackageManagerReady(cancellationToken);
            }

            // Build multipart manually — .NET MultipartFormDataContent reorders headers
            // (Content-Type before Content-Disposition) and adds filename*=utf-8 which
            // WDP Xbox rejects. Manual format matches browser multipart that works.
            // Stream the file via ConcatStream to avoid loading it into memory.
            var boundary = "----XboxUploadBoundary";
            var escapedFileName = fileName.Replace("\"", "\\\"");
            var headerBytes = Encoding.UTF8.GetBytes(
                $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{escapedFileName}\"\r\nContent-Type: application/octet-stream\r\n\r\n");
            var footerBytes = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
            var fileStream = File.OpenRead(filePath);
            var bodyStream = new ConcatStream(
                new MemoryStream(headerBytes, writable: false),
                fileStream,
                new MemoryStream(footerBytes, writable: false));
            var totalLength = headerBytes.Length + fileSize + footerBytes.Length;
            var content = new StreamContent(bodyStream, (int)Math.Min(totalLength, int.MaxValue));
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data")
                { Parameters = { new System.Net.Http.Headers.NameValueHeaderValue("boundary", boundary) } };

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

            // Use per-request timeout for uploads — HttpClient 30s default is far too short
            // for large packages. 10 minutes covers ~1 GB at 2 MB/s; still finite enough
            // to detect genuinely stuck connections.
            var uploadTimeout = TimeSpan.FromMinutes(10);
            using var uploadCts = new CancellationTokenSource(uploadTimeout);
            HttpResponseMessage response;
            try
            {
                response = await _auth.PostWithCsrfAsync(url, content, uploadCts.Token);
            }
            catch (OperationCanceledException) when (uploadCts.IsCancellationRequested)
            {
                Logger.Error($"Upload timed out after {uploadTimeout.TotalMinutes} minutes: {fileName}");
                progress?.Report(new InstallProgressInfo
                {
                    Status = $"Upload timed out: {fileName}",
                    CurrentFile = fileName,
                    File = 0
                });
                continue;
            }

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

    private async Task<bool> WaitForPackageManagerReady(CancellationToken cancellationToken = default)
    {
        Logger.Info("Waiting for package manager to be ready...");
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var resp = await _auth.Http.GetAsync("/api/app/packagemanager/state", cancellationToken);
                var code = (int)resp.StatusCode;
                Logger.Info($"GET /api/app/packagemanager/state => {code}");

                if (XboxResponseParser.IsIdleCode(resp.StatusCode))
                {
                    // 204/404 means no operation in progress
                    await Task.Delay(PollDelayMs, cancellationToken);
                    var resp2 = await _auth.Http.GetAsync("/api/app/packagemanager/state", cancellationToken);
                    var code2 = (int)resp2.StatusCode;
                    Logger.Info($"GET /api/app/packagemanager/state => {code2}");

                    if (XboxResponseParser.IsIdleCode(resp2.StatusCode))
                    {
                        Logger.Info("Package manager ready (got idle status twice)");
                        return true;
                    }

                    // Confirmation poll got 200+JSON — check Success
                    if (resp2.StatusCode == System.Net.HttpStatusCode.OK && XboxResponseParser.IsJsonSuccess(await resp2.Content.ReadAsStringAsync(cancellationToken), out var statusMsg))
                    {
                        Logger.Info($"Package manager ready (idle then success: {statusMsg})");
                        return true;
                    }

                    continue;
                }

                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var body = await resp.Content.ReadAsStringAsync(cancellationToken);
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
                        Logger.Warn($"Package manager blocked — apps need to be closed: {busyApps}");
                        var blockingPfns = ExtractPackageFullNames(busyApps);
                        if (blockingPfns.Count > 0)
                        {
                            foreach (var pf in blockingPfns)
                            {
                                Logger.Info($"Auto-terminating blocking app: {pf}");
                                await TerminatePackageAsync(pf);
                            }
                            await Task.Delay(2000, cancellationToken);
                        }
                        else
                        {
                            Logger.Warn("Could not extract package names from error, waiting 5s");
                            await Task.Delay(5000, cancellationToken);
                        }
                        continue;
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
        catch (OperationCanceledException)
        {
            Logger.Info("Install cancelled by user");
            return false;
        }
        catch (Exception ex)
            {
                Logger.Warn($"Package manager polling error: {ex.Message}");
            }
            await Task.Delay(RetryDelayMs, cancellationToken);
        }
        Logger.Warn("Timed out waiting for package manager");
        return false;
    }

    private static readonly Regex PfnRegex = new(@"(\S+?_\d[\d.]+_\w+__\w+)", RegexOptions.Compiled);

    internal static List<string> ExtractPackageFullNames(string errorText)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(errorText)) return results;
        foreach (Match m in PfnRegex.Matches(errorText))
        {
            var pf = m.Groups[1].Value;
            if (!results.Contains(pf, StringComparer.OrdinalIgnoreCase))
                results.Add(pf);
        }
        return results;
    }

    /// <summary>
    /// Sequential read-only stream concatenation — streams header + file + footer
    /// without loading the file into memory. WDP multipart needs exact byte order.
    /// </summary>
    private sealed class ConcatStream : Stream
    {
        private readonly Stream[] _streams;
        private int _current;

        public ConcatStream(params Stream[] streams) => _streams = streams;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _streams.Sum(s => s.Length);
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count && _current < _streams.Length)
            {
                int read = _streams[_current].Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0) { _current++; continue; }
                totalRead += read;
            }
            return totalRead;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length && _current < _streams.Length)
            {
                int read = await _streams[_current].ReadAsync(buffer[totalRead..], ct);
                if (read == 0) { _current++; continue; }
                totalRead += read;
            }
            return totalRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var s in _streams)
                    s.Dispose();
            base.Dispose(disposing);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
