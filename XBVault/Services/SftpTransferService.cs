using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet.Common;
using XBVault.Helpers;
using XBVault.Models;
using static XBVault.Helpers.FileSystemPathParser;

namespace XBVault.Services;

public readonly record struct TransferUpdate(double Progress, string StatusText);

public class TransferResult
{
    public bool Cancelled { get; init; }
    public bool Success { get; init; }
    public bool IsEmptyResult { get; init; }
    public string? StatusMessage { get; init; }
    public List<SftpEntry> NewEntries { get; init; } = [];
    public int DownloadedCount { get; init; }

    public static TransferResult CancelledResult(string msg) =>
        new() { Cancelled = true, StatusMessage = msg };

    public static TransferResult EmptyResult(string msg) =>
        new() { Success = true, IsEmptyResult = true, StatusMessage = msg };

    public static TransferResult Ok(string msg, List<SftpEntry>? entries = null) =>
        new() { Success = true, StatusMessage = msg, NewEntries = entries ?? [] };

    public static TransferResult OkDownload(string msg, int count) =>
        new() { Success = true, StatusMessage = msg, DownloadedCount = count };

    public static TransferResult Fail(Exception ex, string fallbackMsg) =>
        new()
        {
            Success = false,
            StatusMessage = ex is SshConnectionException ? "Transfer failed — connection lost" : fallbackMsg
        };
}

public class SftpTransferService : IDisposable
{
    private readonly ISftpService _sftp;
    private CancellationTokenSource? _cts;
    private DateTime _transferStartTime;
    private DateTime _overallStartTime;
    private long _transferBytesTotal;
    private string? _currentFileLabel;
    private double _currentFraction;
    private long _currentFileBytes;

    public SftpTransferService(ISftpService sftp)
    {
        _sftp = sftp;
    }

    public bool HasActiveTransfer => _cts is not null;

    public void CancelTransfer()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private CancellationToken Token => _cts?.Token ?? CancellationToken.None;

    private void BeginTransfer()
    {
        _cts = new CancellationTokenSource();
        _transferStartTime = DateTime.UtcNow;
        _overallStartTime = DateTime.UtcNow;
        _transferBytesTotal = 0;
        _currentFileLabel = null;
        _currentFraction = 0;
        _currentFileBytes = 0;
    }

    private void EndTransfer()
    {
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _cts = null;
        GC.SuppressFinalize(this);
    }

    private void Report(IProgress<TransferUpdate>? progress, double value, string status)
        => progress?.Report(new TransferUpdate(value, status));

    private ForwardingProgress MakeProgress(IProgress<TransferUpdate>? progress, Func<double, string> status)
        => new(progress, status, f =>
        {
            _currentFraction = f;
            _currentFileBytes = (long)(f * _transferBytesTotal);
        });

    private string FailureContext(string action, string outcome = "failed")
    {
        var msg = $"{action} {outcome} at {_currentFraction * 100:F0}%";
        if (_currentFileLabel is not null)
            msg += $" file='{_currentFileLabel}'";
        if (_currentFileBytes > 0)
            msg += $" ({FormatBytes(_currentFileBytes)})";
        var elapsed = (DateTime.UtcNow - _transferStartTime).TotalSeconds;
        if (elapsed >= 0.5)
            msg += $" after {elapsed:F1}s";
        return msg;
    }

    private string FormatSpeed(double fraction)
    {
        if (_transferBytesTotal <= 0) return string.Empty;
        var elapsed = (DateTime.UtcNow - _transferStartTime).TotalSeconds;
        if (elapsed < 0.5) return string.Empty;
        return FormatBps(fraction * _transferBytesTotal / elapsed);
    }

    private void LogSummary(string action, int fileCount, long totalBytes)
    {
        var elapsed = (DateTime.UtcNow - _overallStartTime).TotalSeconds;
        if (elapsed < 0.01) return;
        Logger.Info($"{action} complete: {fileCount} file(s), {FormatBytes(totalBytes)} in {elapsed:F1}s avg={FormatBps(totalBytes / elapsed)}");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    /// <summary>
    /// Collects the unique remote directories needed for a batch of relative paths,
    /// sorted parent-first (shallowest first) so mkdir always has its parent ready.
    /// </summary>
    private static List<string> CollectRemoteDirs(string basePath, string[] relativePaths)
    {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Always ensure the target folder itself exists, even when a dropped folder
        // contains only root-level files (no subdirectories to derive dirs from).
        var baseRemote = basePath.Replace('\\', '/');
        dirs.Add(baseRemote);
        seen.Add(baseRemote);
        foreach (var relative in relativePaths)
        {
            var sep = relative.LastIndexOf('\\');
            if (sep <= 0) continue;
            var dir = (basePath + "\\" + relative[..sep]).Replace('\\', '/');
            if (seen.Add(dir)) dirs.Add(dir);
        }
        dirs.Sort((a, b) => a.Count(c => c == '/').CompareTo(b.Count(c => c == '/')));
        return dirs;
    }

    private async Task CreateDirsAsync(List<string> dirs, IProgress<TransferUpdate>? progress, int totalUnits, CancellationToken ct)
    {
        for (int d = 0; d < dirs.Count; d++)
        {
            ct.ThrowIfCancellationRequested();
            Report(progress, (double)d / totalUnits, $"Creating folders ({d + 1}/{dirs.Count})...");
            await _sftp.CreateDirectoryAsync(dirs[d]);
        }
    }

    private sealed class ForwardingProgress : IProgress<double>
    {
        private readonly IProgress<TransferUpdate>? _target;
        private readonly Func<double, string> _status;
        private readonly Action<double>? _onTick;

        public ForwardingProgress(IProgress<TransferUpdate>? target, Func<double, string> status, Action<double>? onTick = null)
        {
            _target = target;
            _status = status;
            _onTick = onTick;
        }

        public void Report(double value)
        {
            _onTick?.Invoke(value);
            _target?.Report(new TransferUpdate(value, _status(value)));
        }
    }

    // ---- Upload ----

    public async Task<TransferResult> UploadFilesAsync(string[] filePaths, string targetPath, IProgress<TransferUpdate>? progress)
    {
        BeginTransfer();
        var ct = Token;
        var newEntries = new List<SftpEntry>();
        string? lastFile = null;
        string? lastRemotePath = null;
        var totalFiles = filePaths.Length;
        long totalBytes = 0;
        Logger.Info($"Upload started: {totalFiles} file(s) -> '{targetPath}'");
        try
        {
            foreach (var filePath in filePaths)
            {
                ct.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(filePath);
                lastFile = fileName;
                Report(progress, 0, $"Uploading {fileName}... (0%)");

                var remotePath = targetPath.TrimEnd('\\') + "\\" + fileName;
                lastRemotePath = remotePath;
                _currentFileLabel = fileName;
                _currentFraction = 0;
                _currentFileBytes = 0;
                await using var stream = File.OpenRead(filePath);
                _transferBytesTotal = stream.Length;
                totalBytes += stream.Length;
                Logger.Debug($"Upload: {fileName} ({stream.Length}B)");
                var p = MakeProgress(progress,
                    f => $"Uploading {fileName}... ({f * 100:F0}%){FormatSpeed(f)}");
                await _sftp.UploadFileAsync(stream, remotePath, p, ct);

                var fi = new FileInfo(filePath);
                newEntries.Add(new SftpEntry
                {
                    Name = fileName,
                    FullPath = remotePath,
                    IsDirectory = false,
                    Size = fi.Length,
                    LastModified = fi.LastWriteTimeUtc
                });
                Report(progress, 1, $"Uploading {fileName}... (100%)");
            }
            LogSummary("Upload", totalFiles, totalBytes);
            return TransferResult.Ok($"{lastFile} uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn(FailureContext("Upload", "cancelled"));
            if (lastRemotePath is not null)
            {
                try { await _sftp.DeleteFileAsync(lastRemotePath); }
                catch { /* best-effort cleanup */ }
            }
            return TransferResult.CancelledResult("Upload cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, FailureContext("Upload"));
            return TransferResult.Fail(ex, $"Upload failed: {ex.Message}");
        }
        finally
        {
            EndTransfer();
        }
    }

    public async Task<TransferResult> UploadFolderAsync(string localFolder, string targetPath, IProgress<TransferUpdate>? progress)
    {
        BeginTransfer();
        var ct = Token;
        var newEntries = new List<SftpEntry>();
        try
        {
            var allFiles = Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories);
            if (allFiles.Length == 0)
                return TransferResult.EmptyResult("Empty folder — nothing to upload");

            var totalFiles = allFiles.Length;
            var folderRoot = localFolder.TrimEnd('\\', '/');
            var folderName = Path.GetFileName(localFolder.TrimEnd('\\'));
            var basePath = targetPath.TrimEnd('\\') + "\\" + folderName;

            Logger.Info($"Upload started: folder '{localFolder}' ({totalFiles} file(s)) -> '{targetPath}'");

            // Pre-create remote dirs once (deduped, parent-first) to avoid per-file round trips
            var relativePaths = new string[totalFiles];
            for (int i = 0; i < totalFiles; i++)
                relativePaths[i] = allFiles[i].Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\');
            var dirs = CollectRemoteDirs(basePath, relativePaths);
            var totalUnits = dirs.Count + totalFiles;
            await CreateDirsAsync(dirs, progress, totalUnits, ct);

            long totalBytes = 0;
            for (int i = 0; i < totalFiles; i++)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = allFiles[i];
                var relative = relativePaths[i];
                var remotePath = basePath + "\\" + relative;

                _transferStartTime = DateTime.UtcNow;
                Report(progress, (double)(dirs.Count + i) / totalUnits, $"Uploading {relative}...");

                await using var stream = File.OpenRead(filePath);
                _transferBytesTotal = stream.Length;
                totalBytes += stream.Length;
                Logger.Debug($"Upload: [{i + 1}/{totalFiles}] {relative} ({stream.Length}B)");
                _currentFileLabel = relative;
                _currentFraction = 0;
                _currentFileBytes = 0;
                var p = MakeProgress(progress,
                    f => $"Uploading {relative}... ({f * 100:F0}%){FormatSpeed(f)}");
                await _sftp.UploadFileAsync(stream, remotePath, p, ct);
            }

            newEntries.Add(new SftpEntry
            {
                Name = Path.GetFileName(localFolder.TrimEnd('\\')),
                FullPath = targetPath.TrimEnd('\\') + "\\" + Path.GetFileName(localFolder.TrimEnd('\\')),
                IsDirectory = true,
                Children = { new SftpEntry { Name = "" } }
            });

            Report(progress, 1, string.Empty);
            LogSummary("Upload", totalFiles, totalBytes);
            return TransferResult.Ok($"{totalFiles} files uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn(FailureContext("Upload", "cancelled"));
            return TransferResult.CancelledResult("Upload cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, FailureContext("Upload"));
            return TransferResult.Fail(ex, $"Upload failed: {ex.Message}");
        }
        finally
        {
            EndTransfer();
        }
    }

    public async Task<TransferResult> UploadMixedAsync(string[]? filePaths, string[]? folderPaths, string targetPath, IProgress<TransferUpdate>? progress)
    {
        BeginTransfer();
        var ct = Token;
        var newEntries = new List<SftpEntry>();
        var fCount = filePaths?.Length ?? 0;
        var dCount = folderPaths?.Length ?? 0;
        var totalItems = fCount + dCount;
        Logger.Info($"Upload started: {fCount} file(s), {dCount} folder(s) -> '{targetPath}'");
        try
        {
            long totalBytes = 0;
            var uploadedItems = 0;

            if (filePaths is not null)
            {
                for (int fi = 0; fi < filePaths.Length; fi++)
                {
                    ct.ThrowIfCancellationRequested();
                    var filePath = filePaths[fi];
                    var fileName = Path.GetFileName(filePath);
                    _transferStartTime = DateTime.UtcNow;
                    Report(progress, (double)uploadedItems / totalItems, $"Uploading {fileName}...");

                    var remotePath = targetPath.TrimEnd('\\') + "\\" + fileName;
                    _currentFileLabel = fileName;
                    _currentFraction = 0;
                    _currentFileBytes = 0;
                    await using var stream = File.OpenRead(filePath);
                    _transferBytesTotal = stream.Length;
                    totalBytes += stream.Length;
                    Logger.Debug($"Upload: {fileName} ({stream.Length}B)");
                    var p = MakeProgress(progress,
                        f => $"Uploading {fileName}... ({f * 100:F0}%){FormatSpeed(f)}");
                    await _sftp.UploadFileAsync(stream, remotePath, p, ct);
                    uploadedItems++;

                    var fiInfo = new FileInfo(filePath);
                    newEntries.Add(new SftpEntry
                    {
                        Name = fileName, FullPath = remotePath,
                        IsDirectory = false, Size = fiInfo.Length, LastModified = fiInfo.LastWriteTimeUtc
                    });
                }
            }

            if (folderPaths is not null)
            {
                foreach (var folderPath in folderPaths)
                {
                    ct.ThrowIfCancellationRequested();
                    var folderName = Path.GetFileName(folderPath.TrimEnd('\\', '/'));
                    var index = uploadedItems;

                    Report(progress, (double)index / totalItems, $"Scanning {folderName}...");

                    var allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                    var folderRoot = folderPath.TrimEnd('\\', '/');
                    var basePath = targetPath.TrimEnd('\\') + "\\" + folderName;

                    var relativePaths = allFiles
                        .Select(f => f.Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\'))
                        .ToArray();
                    var dirs = CollectRemoteDirs(basePath, relativePaths);
                    await CreateDirsAsync(dirs, progress, totalItems, ct);

                    foreach (var filePath in allFiles)
                    {
                        ct.ThrowIfCancellationRequested();
                        var relative = filePath.Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\');
                        var remotePath = basePath + "\\" + relative;

                        _transferStartTime = DateTime.UtcNow;
                        Report(progress, (double)uploadedItems / totalItems, $"Uploading {relative}...");
                        await using var stream = File.OpenRead(filePath);
                        _transferBytesTotal = stream.Length;
                        totalBytes += stream.Length;
                        Logger.Debug($"Upload: {relative} ({stream.Length}B)");
                        _currentFileLabel = relative;
                        _currentFraction = 0;
                        _currentFileBytes = 0;
                        var p = MakeProgress(progress,
                            f => $"Uploading {relative}... ({f * 100:F0}%){FormatSpeed(f)}");
                        await _sftp.UploadFileAsync(stream, remotePath, p, ct);
                    }
                    uploadedItems++;

                    Report(progress, (double)(index + 1) / totalItems, string.Empty);
                    newEntries.Add(new SftpEntry
                    {
                        Name = folderName, FullPath = targetPath.TrimEnd('\\') + "\\" + folderName,
                        IsDirectory = true, Children = { new SftpEntry { Name = "" } }
                    });
                }
            }

            Report(progress, 1, string.Empty);
            LogSummary("Upload", totalItems, totalBytes);
            return TransferResult.Ok($"{totalItems} item(s) uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn(FailureContext("Upload", "cancelled"));
            return TransferResult.CancelledResult("Upload cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, FailureContext("Upload"));
            return TransferResult.Fail(ex, $"Upload failed: {ex.Message}");
        }
        finally
        {
            EndTransfer();
        }
    }

    public async Task<TransferResult> UploadZipExtractAsync(string zipPath, string targetPath, IProgress<TransferUpdate>? progress)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "XBVault", Path.GetFileNameWithoutExtension(zipPath));
        Directory.CreateDirectory(tempDir);
        var newEntries = new List<SftpEntry>();
        try
        {
            var extractSw = Stopwatch.StartNew();
            ZipFile.ExtractToDirectory(zipPath, tempDir);
            var allFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
            extractSw.Stop();
            if (allFiles.Length == 0)
                return TransferResult.EmptyResult("Empty ZIP — nothing to upload");

            Logger.Info($"ZIP upload started: '{zipPath}' ({allFiles.Length} file(s), extracted in {extractSw.ElapsedMilliseconds}ms) -> '{targetPath}'");

            BeginTransfer();
            var ct = Token;
            var totalFiles = allFiles.Length;
            var folderRoot = tempDir.TrimEnd('\\', '/');
            var basePath = targetPath.TrimEnd('\\');

            // Pre-create remote dirs once (deduped, parent-first) to avoid per-file round trips
            var relativePaths = new string[totalFiles];
            for (int i = 0; i < totalFiles; i++)
                relativePaths[i] = allFiles[i].Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\');
            var dirs = CollectRemoteDirs(basePath, relativePaths);
            var totalUnits = dirs.Count + totalFiles;
            await CreateDirsAsync(dirs, progress, totalUnits, ct);

            long totalBytes = 0;
            for (int i = 0; i < totalFiles; i++)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = allFiles[i];
                var relative = relativePaths[i];
                var remotePath = basePath + "\\" + relative;

                _transferStartTime = DateTime.UtcNow;
                Report(progress, (double)(dirs.Count + i) / totalUnits, $"Extracting & uploading {relative}...");

                await using var stream = File.OpenRead(filePath);
                _transferBytesTotal = stream.Length;
                totalBytes += stream.Length;
                Logger.Debug($"Upload: [{i + 1}/{totalFiles}] {relative} ({stream.Length}B)");
                _currentFileLabel = relative;
                _currentFraction = 0;
                _currentFileBytes = 0;
                var p = MakeProgress(progress,
                    f => $"Extracting & uploading {relative}... ({f * 100:F0}%){FormatSpeed(f)}");
                await _sftp.UploadFileAsync(stream, remotePath, p, ct);
            }

            var addedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < totalFiles; i++)
            {
                var rel = allFiles[i].Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\');
                var remotePath = targetPath.TrimEnd('\\') + "\\" + rel;
                var lastSep = rel.LastIndexOf('\\');
                var dirPart = lastSep > 0 ? rel[..lastSep] : string.Empty;

                if (!string.IsNullOrEmpty(dirPart))
                {
                    var acc = targetPath.TrimEnd('\\');
                    foreach (var part in dirPart.Split('\\'))
                    {
                        acc += "\\" + part;
                        if (addedDirs.Add(acc))
                            newEntries.Add(new SftpEntry
                            {
                                Name = part, FullPath = acc,
                                IsDirectory = true, Children = { new SftpEntry { Name = "" } }
                            });
                    }
                }
                else
                {
                    var fiEntry = new FileInfo(allFiles[i]);
                    newEntries.Add(new SftpEntry
                    {
                        Name = rel, FullPath = remotePath,
                        IsDirectory = false, Size = fiEntry.Length,
                        LastModified = fiEntry.LastWriteTimeUtc
                    });
                }
            }

            Report(progress, 1, string.Empty);
            LogSummary("ZIP upload", totalFiles, totalBytes);
            return TransferResult.Ok($"{totalFiles} files extracted and uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn(FailureContext("ZIP upload", "cancelled"));
            return TransferResult.CancelledResult("ZIP upload cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, FailureContext("ZIP upload"));
            return TransferResult.Fail(ex, $"ZIP upload failed: {ex.Message}");
        }
        finally
        {
            EndTransfer();
            try { Directory.Delete(tempDir, true); }
            catch (Exception ex) { Logger.Warn($"Failed to clean temp folder: {tempDir} — {ex.Message}"); }
        }
    }

    // ---- Download ----

    public async Task<TransferResult> DownloadFilesAsync(IReadOnlyList<SftpEntry> entries, string localDir, IProgress<TransferUpdate>? progress)
    {
        BeginTransfer();
        var ct = Token;
        var fileList = new List<(SftpEntry Entry, string RelativePath)>();
        try
        {
            var listSw = Stopwatch.StartNew();
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (!entry.IsDirectory)
                {
                    fileList.Add((entry, entry.Name));
                }
                else
                {
                    try
                    {
                        Report(progress, 0, $"Scanning {entry.Name}...");
                        var all = await _sftp.RecursiveListAsync(entry.FullPath);
                        var folderRoot = entry.FullPath.TrimEnd('\\');
                        foreach (var file in all.Where(e => !e.IsDirectory))
                        {
                            var relRemote = file.FullPath.Substring(folderRoot.Length).TrimStart('\\');
                            var relative = Path.Combine(entry.Name.TrimEnd('\\'), relRemote.Replace('\\', Path.DirectorySeparatorChar));
                            fileList.Add((file, relative));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed to scan folder: {entry.Name}");
                    }
                }
            }

            var totalFiles = fileList.Count;
            if (totalFiles == 0)
                return TransferResult.EmptyResult("Nothing to download");

            Logger.Info($"Download started: {totalFiles} file(s) (listed in {listSw.ElapsedMilliseconds}ms) -> '{localDir}'");

            string? partialPath = null;
            _transferStartTime = DateTime.UtcNow;
            long totalBytes = 0;
            try
            {
                for (int i = 0; i < totalFiles; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var (file, relative) = fileList[i];
                    _transferStartTime = DateTime.UtcNow;
                    _transferBytesTotal = await _sftp.GetFileSizeAsync(file.FullPath);
                    totalBytes += Math.Max(0, _transferBytesTotal);
                    Logger.Debug($"Download: [{i + 1}/{totalFiles}] {relative} ({_transferBytesTotal}B)");
                    Report(progress, (double)i / totalFiles, $"Downloading {file.Name}...");

                    partialPath = Path.Combine(localDir, relative);
                    var parentDir = Path.GetDirectoryName(partialPath);
                    if (!string.IsNullOrEmpty(parentDir))
                        Directory.CreateDirectory(parentDir);
                    _currentFileLabel = relative;
                    _currentFraction = 0;
                    _currentFileBytes = 0;

                    await using var stream = File.Create(partialPath);
                    var p = MakeProgress(progress,
                        f => $"Downloading {file.Name}... ({f * 100:F0}%){FormatSpeed(f)}");
                    await _sftp.DownloadFileAsync(file.FullPath, stream, p, ct);

                    Report(progress, (double)(i + 1) / totalFiles, $"{file.Name} downloaded ({i + 1}/{totalFiles})");
                    partialPath = null;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn(FailureContext("Download", "cancelled"));
                if (partialPath is not null && File.Exists(partialPath))
                    File.Delete(partialPath);
                return TransferResult.CancelledResult("Download cancelled");
            }

            LogSummary("Download", totalFiles, totalBytes);
            return TransferResult.OkDownload($"{totalFiles} files downloaded", totalFiles);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn(FailureContext("Download", "cancelled"));
            return TransferResult.CancelledResult("Download cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, FailureContext("Download"));
            return TransferResult.Fail(ex, $"Download failed: {ex.Message}");
        }
        finally
        {
            EndTransfer();
        }
    }

    public async Task<TransferResult> DownloadSingleFileAsync(SftpEntry entry, string savePath, IProgress<TransferUpdate>? progress)
    {
        BeginTransfer();
        var ct = Token;
        try
        {
            _transferStartTime = DateTime.UtcNow;
            _transferBytesTotal = await _sftp.GetFileSizeAsync(entry.FullPath);
            _currentFileLabel = entry.Name;
            _currentFraction = 0;
            _currentFileBytes = 0;
            Logger.Info($"Download started: '{entry.Name}' ({_transferBytesTotal}B) -> '{savePath}'");
            Report(progress, 0, $"Downloading {entry.Name}... (0%)");

            await using var stream = File.Create(savePath);
            var p = MakeProgress(progress,
                f => $"Downloading {entry.Name}... ({f * 100:F0}%){FormatSpeed(f)}");
            _transferBytesTotal = await _sftp.DownloadFileAsync(entry.FullPath, stream, p, ct);

            LogSummary("Download", 1, _transferBytesTotal);
            return TransferResult.OkDownload($"{entry.Name} downloaded", 1);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn(FailureContext("Download", "cancelled"));
            if (File.Exists(savePath))
                File.Delete(savePath);
            return TransferResult.CancelledResult($"{entry.Name} cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, FailureContext("Download"));
            return TransferResult.Fail(ex, $"Download failed: {entry.Name}: {ex.Message}");
        }
        finally
        {
            EndTransfer();
        }
    }

    public async Task<TransferResult> DownloadFolderAsync(SftpEntry entry, string localRoot, IProgress<TransferUpdate>? progress)
    {
        try
        {
            var listSw = Stopwatch.StartNew();
            Report(progress, 0, "Listing files...");
            var allEntries = await _sftp.RecursiveListAsync(entry.FullPath);
            var files = allEntries.Where(e => !e.IsDirectory).ToList();
            var totalFiles = files.Count;
            listSw.Stop();

            if (totalFiles == 0)
                return TransferResult.EmptyResult("Empty folder — nothing to download");

            Logger.Info($"Download started: folder '{entry.Name}' ({totalFiles} file(s), listed in {listSw.ElapsedMilliseconds}ms) -> '{localRoot}'");

            BeginTransfer();
            var ct = Token;
            localRoot = Path.Combine(localRoot, entry.Name.TrimEnd('\\'));
            Directory.CreateDirectory(localRoot);
            var rootPath = entry.FullPath.TrimEnd('\\');
            string? partialPath = null;

            _transferStartTime = DateTime.UtcNow;
            long totalBytes = 0;
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var file = files[i];
                    var relative = file.FullPath.Substring(rootPath.Length).TrimStart('\\').Replace('\\', Path.DirectorySeparatorChar);
                    partialPath = Path.Combine(localRoot, relative);
                    var localDir = Path.GetDirectoryName(partialPath);
                    if (!string.IsNullOrEmpty(localDir))
                        Directory.CreateDirectory(localDir);
                    _currentFileLabel = relative;
                    _currentFraction = 0;
                    _currentFileBytes = 0;

                    _transferStartTime = DateTime.UtcNow;
                    _transferBytesTotal = await _sftp.GetFileSizeAsync(file.FullPath);
                    totalBytes += Math.Max(0, _transferBytesTotal);
                    Logger.Debug($"Download: [{i + 1}/{totalFiles}] {relative} ({_transferBytesTotal}B)");
                    Report(progress, (double)i / totalFiles, $"Downloading {file.Name}...");

                    await using var stream = File.Create(partialPath);
                    var p = MakeProgress(progress,
                        f => $"Downloading {file.Name}... ({f * 100:F0}%){FormatSpeed(f)}");
                    await _sftp.DownloadFileAsync(file.FullPath, stream, p, ct);

                    Report(progress, (double)(i + 1) / totalFiles, string.Empty);
                    partialPath = null;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn(FailureContext($"Download folder '{entry.Name}'", "cancelled"));
                if (partialPath is not null && File.Exists(partialPath))
                    File.Delete(partialPath);
                return TransferResult.CancelledResult($"{entry.Name} cancelled");
            }

            LogSummary("Download", totalFiles, totalBytes);
            return TransferResult.OkDownload($"{totalFiles} files downloaded", totalFiles);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, FailureContext($"Download folder '{entry.Name}'"));
            return TransferResult.Fail(ex, $"Folder download failed: {entry.Name}: {ex.Message}");
        }
        finally
        {
            EndTransfer();
        }
    }
}
