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
    // Time budgets. Instance settable so integration tests can shrink them (and the
    // poll delays) to run fast against a stubbed HTTP layer.
    internal TimeSpan MainPollTimeout { get; set; } = TimeSpan.FromSeconds(40);
    internal TimeSpan DepPollTimeout { get; set; } = TimeSpan.FromSeconds(10);
    internal TimeSpan IdlePollTimeout { get; set; } = TimeSpan.FromSeconds(20);
    internal TimeSpan PollDelay { get; set; } = TimeSpan.FromSeconds(2);
    internal TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(3);

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

        var totalFiles = 1 + dependencies.Length;
        var mainName = Path.GetFileName(packagePath);
        var targetIdentity = XboxResponseParser.ParseMsixPackageName(packagePath)
            ?? Path.GetFileNameWithoutExtension(packagePath);

        try
        {
            // Kill any running instance of this package before upload
            await TerminateRunningPackageAsync(packagePath);

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

            // Let the main deploy settle before uploading dependencies.
            var mainWait = await WaitForPackageManagerReady(PmWaitMode.AwaitDeployMain, targetIdentity, cancellationToken, MainPollTimeout);
            if (mainWait == PackageManagerWaitResult.Failed)
            {
                Logger.Error($"Main package install reported failure: {mainName}");
                return await ResolveFinalResultAsync(mainName, targetIdentity);
            }

            // Upload dependencies one at a time. A dependency is a framework (VCLibs,
            // .NET Native, UI.Xaml...) — the Xbox installs them system-wide and they can
            // never be listed by /packagemanager/packages (that endpoint returns registered
            // apps only). When the deployment reports 0x80073D02 for a dependency, the
            // framework is ALREADY installed and held in use by a running app; redeploying
            // it can never succeed (the blocker, e.g. DevHome, is the Dev Mode shell and
            // must not be terminated). Detected via the state poll, so we skip, never kill.
            Logger.Info($"Uploading {dependencies.Length} dependencies...");
            var depIndex = 0;
            var skippedDependencies = 0;
            var failedDependencies = 0;
            foreach (var dep in dependencies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                depIndex++;
                if (!File.Exists(dep))
                {
                    Logger.Warn($"Dependency not found: {dep}");
                    failedDependencies++;
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

                // Make sure the manager is idle before the next upload (409 backoff also guards this).
                await WaitForPackageManagerReady(PmWaitMode.AwaitIdle, "", cancellationToken, IdlePollTimeout);

                var depOk = await UploadAppxFile(dep, progress, cancellationToken);
                if (!depOk)
                {
                    Logger.Error($"  Dependency failed: {depName}");
                    failedDependencies++;
                    continue;
                }

                var depWait = await WaitForPackageManagerReady(PmWaitMode.AwaitDeployDep, "", cancellationToken, DepPollTimeout);
                switch (depWait)
                {
                    case PackageManagerWaitResult.ResourceInUse:
                        Logger.Warn($"  Dependency already installed system-wide, skipped: {depName}");
                        skippedDependencies++;
                        continue;
                    case PackageManagerWaitResult.Cancelled:
                        return await ResolveFinalResultAsync(mainName, targetIdentity, skippedDependencies, failedDependencies);
                    case PackageManagerWaitResult.Failed:
                    case PackageManagerWaitResult.TimedOut:
                        Logger.Warn($"  Dependency deploy unresolved ({depWait}): {depName} — continuing; final check decides");
                        failedDependencies++;
                        continue;
                    default:
                        Logger.Info($"  Dependency installed: {depName}");
                        break;
                }
            }

            // Final settle wait, then the AUTHORITATIVE verdict: query the installed-packages
            // API directly (it lists registered apps, including the target). Deliberately NOT
            // cancelled by the caller's token — an aborted/hung install must still report
            // whether the app actually landed, instead of a misleading failure.
            var finalWait = await WaitForPackageManagerReady(PmWaitMode.AwaitDeployMain, targetIdentity, cancellationToken, MainPollTimeout);
            Logger.Info(finalWait == PackageManagerWaitResult.Ready
                ? "Package manager settled"
                : $"Package manager final state: {finalWait}. Checking installed packages...");

            return await ResolveFinalResultAsync(mainName, targetIdentity, skippedDependencies, failedDependencies);
        }
        catch (OperationCanceledException)
        {
            // User aborted mid-deploy — resolve the true result via the installed-packages
            // API instead of blindly reporting failure.
            return await ResolveFinalResultAsync(Path.GetFileName(packagePath), targetIdentity);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Install failed for {packagePath}");
            return false;
        }
    }

    /// <summary>
    /// Always-run authoritative check. After the whole install/update flow, asks the
    /// installed-packages API whether the target app is present, and reports success
    /// whenever it is — regardless of dependency install outcomes (a dependency that
    /// could not be deployed was, by construction of 0x80073D02, already present on
    /// the console). Uses no caller token, so a user abort does not turn an installed
    /// app into a "failed" result.
    /// </summary>
    private async Task<bool> ResolveFinalResultAsync(string mainName, string targetIdentity, int skippedDependencies = 0, int failedDependencies = 0)
    {
        var present = false;
        try
        {
            var installed = await GetInstalledPackagesAsync();
            var found = installed.FirstOrDefault(p =>
                p.FullName?.StartsWith(targetIdentity + "_", StringComparison.OrdinalIgnoreCase) == true);
            present = found is not null;
            if (found is not null)
                Logger.Info($"Install verified via installed-packages API: {found.FullName}");
        }
        catch (Exception verifyEx)
        {
            Logger.Warn($"Final install verification failed: {verifyEx.Message}");
        }

        if (present)
        {
            if (skippedDependencies > 0)
                Logger.Info($"Install result: SUCCESS ({mainName} installed). {skippedDependencies} dependenc(ies) already present on console, skipped{SuccessSuffix(failedDependencies)}");
            else if (failedDependencies > 0)
                Logger.Info($"Install result: SUCCESS ({mainName} installed) despite {failedDependencies} failed dependenc(ies)");
            else
                Logger.Info($"Install result: SUCCESS ({mainName} installed)");
            return true;
        }

        Logger.Error($"Install result: FAILED — {mainName} not present in installed packages");
        return false;
    }

    private static string SuccessSuffix(int failedDependencies)
        => failedDependencies > 0 ? $" ({failedDependencies} failed)" : "";

    private async Task<bool> UploadAppxFile(string filePath, IProgress<InstallProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = new FileInfo(filePath).Length;
        Logger.Info($"Uploading: {fileName} ({XboxResponseParser.SizeFormat(fileSize)})");

        for (int attempt = 0; attempt <= 3; attempt++)
        {
            if (attempt > 0)
            {
                var wait = attempt * 5;
                Logger.Info($"Waiting {wait}s (Xbox busy, attempt {attempt}/3)...");
                progress?.Report(new InstallProgressInfo
                {
                    Status = $"Waiting for previous install to finish ({wait}s)...",
                    CurrentFile = fileName
                });
                await Task.Delay(TimeSpan.FromSeconds(wait), cancellationToken);
                await WaitForPackageManagerReady(PmWaitMode.AwaitIdle, "", cancellationToken, IdlePollTimeout);
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
            using var content = new StreamContent(bodyStream, (int)Math.Min(totalLength, int.MaxValue));
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
            // to detect genuinely stuck connections. Linked to the caller's token so a
            // user abort actually cancels the in-flight upload instead of letting it run.
            var uploadTimeout = TimeSpan.FromMinutes(10);
            using var uploadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            uploadCts.CancelAfter(uploadTimeout);
            HttpResponseMessage response;
            try
            {
                response = await _auth.PostWithCsrfAsync(url, content, uploadCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.Info($"Upload cancelled by user: {fileName}");
                progress?.Report(new InstallProgressInfo
                {
                    Status = "Install cancelled",
                    CurrentFile = fileName,
                    File = 0
                });
                throw;
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

    internal enum PackageManagerWaitResult
    {
        Ready,
        ResourceInUse,
        Cancelled,
        Failed,
        TimedOut
    }

    private enum PmWaitMode
    {
        /// <summary>Just need the manager idle before the next upload — never terminate anything.</summary>
        AwaitIdle,
        /// <summary>Waiting for the main package deploy to settle — may terminate ONLY the target being installed.</summary>
        AwaitDeployMain,
        /// <summary>Waiting for a dependency deploy. 0x80073D02 here means the framework is already installed and in use; return it to the caller as "present, skip".</summary>
        AwaitDeployDep
    }

    private async Task<PackageManagerWaitResult> WaitForPackageManagerReady(
        PmWaitMode mode,
        string targetIdentity,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        Logger.Info("Waiting for package manager to be ready...");
        var deadline = DateTime.UtcNow.Add(timeout ?? MainPollTimeout);
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
                    await Task.Delay(PollDelay, cancellationToken);
                    var resp2 = await _auth.Http.GetAsync("/api/app/packagemanager/state", cancellationToken);
                    var code2 = (int)resp2.StatusCode;
                    Logger.Info($"GET /api/app/packagemanager/state => {code2}");

                    if (XboxResponseParser.IsIdleCode(resp2.StatusCode))
                    {
                        if (mode == PmWaitMode.AwaitDeployDep)
                        {
                            // A dependency deploy was just accepted (202) — the first /state
                            // poll can still hit idle before the manager registers the op. A dep
                            // is only "ready" on an explicit terminal state (success JSON, D02,
                            // signature, fatal); a bare idle-twice would mislabel it installed.
                            Logger.Info("Package manager idle (dependency deploy not yet recognized)");
                            continue;
                        }
                        Logger.Info("Package manager ready (got idle status twice)");
                        return PackageManagerWaitResult.Ready;
                    }

                    // Confirmation poll got 200+JSON — check Success
                    if (resp2.StatusCode == System.Net.HttpStatusCode.OK && XboxResponseParser.IsJsonSuccess(await resp2.Content.ReadAsStringAsync(cancellationToken), out var statusMsg))
                    {
                        Logger.Info($"Package manager ready (idle then success: {statusMsg})");
                        return PackageManagerWaitResult.Ready;
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
                        return PackageManagerWaitResult.Ready;
                    }

                    if (XboxResponseParser.IsSignatureError(body))
                    {
                        Logger.Info("Package manager ready (TRUST_E_NOSIGNATURE — no operation in progress)");
                        return PackageManagerWaitResult.Ready;
                    }

                    if (XboxResponseParser.IsResourceInUseError(body, out var busyApps))
                    {
                        // 0x80073D02 — resources held by a running process. A process can only hold
                        // resources "in use" on files that exist, so a dependency flagged this way is
                        // ALREADY INSTALLED on the system (a genuinely missing framework surfaces as
                        // 0x80073CF3 instead) and the blocker is often the Dev Mode shell. Never kill
                        // a non-target; the whole point of this redesign is to stop killing DevHome.
                        if (mode == PmWaitMode.AwaitDeployDep)
                        {
                            Logger.Warn($"Dependency blocked by app in use: {busyApps} — framework already installed system-wide, skipping");
                            return PackageManagerWaitResult.ResourceInUse;
                        }

                        var blockingPfns = ExtractPackageFullNames(busyApps);
                        var targets = FilterBlockingTargets(targetIdentity, blockingPfns);
                        if (targets.Count > 0)
                        {
                            foreach (var pf in targets)
                            {
                                Logger.Info($"Terminating target app being updated: {pf}");
                                await TerminatePackageAsync(pf);
                            }
                            await Task.Delay(PollDelay, cancellationToken);
                        }
                        else
                        {
                            if (mode == PmWaitMode.AwaitDeployMain)
                            {
                                // D02 naming only non-target blockers = a framework already
                                // installed and held in use (e.g. the Dev Mode shell). It can
                                // never self-resolve while that app runs, so polling the full
                                // MainPollTimeout only burns ~40s ahead of the authoritative
                                // verdict from ResolveFinalResultAsync (installed-packages check).
                                Logger.Warn($"Package manager blocked by non-target app(s) — settling early, final check decides: {busyApps}");
                                return PackageManagerWaitResult.Ready;
                            }
                            Logger.Warn($"Blocked by app(s) not targeted by this install: {busyApps}");
                            if (mode == PmWaitMode.AwaitIdle)
                                Logger.Info("  (waiting — no termination attempted)");
                            await Task.Delay(PollDelay, cancellationToken);
                        }
                        continue;
                    }

                    if (XboxResponseParser.IsHigherVersionError(body, out var higherVerMsg))
                    {
                        Logger.Warn($"Skipped (higher version already installed): {higherVerMsg}");
                        return PackageManagerWaitResult.Ready;
                    }

                    if (XboxResponseParser.IsFatalDeploymentError(body, out var deployError))
                    {
                        Logger.Error($"Package manager deployment failed: {deployError}");
                        return PackageManagerWaitResult.Failed;
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
                return PackageManagerWaitResult.Cancelled;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Package manager polling error: {ex.Message}");
            }
            await Task.Delay(RetryDelay, cancellationToken);
        }
        Logger.Warn("Timed out waiting for package manager");
        return PackageManagerWaitResult.TimedOut;
    }

    /// <summary>
    /// From a set of blocking package full names reported by 0x80073D02, keep only those that
    /// belong to the package being installed/updated (identity prefix). Everything else — the
    /// Dev Mode shell, IdleScreen, dashboard, games the user is running — must never be killed.
    /// </summary>
    internal static List<string> FilterBlockingTargets(string targetIdentity, List<string> blockingPfns)
    {
        if (string.IsNullOrEmpty(targetIdentity) || blockingPfns.Count == 0)
            return [];
        return blockingPfns
            .Where(pf => pf.StartsWith(targetIdentity + "_", StringComparison.OrdinalIgnoreCase))
            .ToList();
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
