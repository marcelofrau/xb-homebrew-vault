using System;
using System.Collections.Generic;
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
    private long _transferBytesTotal;

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
        _transferBytesTotal = 0;
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

    private string FormatSpeed(double fraction)
    {
        if (_transferBytesTotal <= 0) return string.Empty;
        var elapsed = (DateTime.UtcNow - _transferStartTime).TotalSeconds;
        if (elapsed < 0.5) return string.Empty;
        return FormatBps(fraction * _transferBytesTotal / elapsed);
    }

    private sealed class ForwardingProgress : IProgress<double>
    {
        private readonly IProgress<TransferUpdate>? _target;
        private readonly Func<double, string> _status;

        public ForwardingProgress(IProgress<TransferUpdate>? target, Func<double, string> status)
        {
            _target = target;
            _status = status;
        }

        public void Report(double value) => _target?.Report(new TransferUpdate(value, _status(value)));
    }

    // ---- Upload ----

    public async Task<TransferResult> UploadFilesAsync(string[] filePaths, string targetPath, IProgress<TransferUpdate>? progress)
    {
        BeginTransfer();
        var ct = Token;
        var newEntries = new List<SftpEntry>();
        string? lastFile = null;
        string? lastRemotePath = null;
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
                await using var stream = File.OpenRead(filePath);
                _transferBytesTotal = stream.Length;
                var p = new ForwardingProgress(progress,
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
            return TransferResult.Ok($"{lastFile} uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            if (lastRemotePath is not null)
            {
                try { await _sftp.DeleteFileAsync(lastRemotePath); }
                catch { /* best-effort cleanup */ }
            }
            return TransferResult.CancelledResult("Upload cancelled");
        }
        catch (Exception ex)
        {
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

            for (int i = 0; i < totalFiles; i++)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = allFiles[i];
                var relative = filePath.Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\');
                var remotePath = targetPath.TrimEnd('\\') + "\\" + Path.GetFileName(localFolder).TrimEnd('\\') + "\\" + relative;
                var lastSep = remotePath.LastIndexOf('\\');
                var remoteDir = lastSep > 0 ? remotePath[..lastSep].Replace('\\', '/') : string.Empty;

                _transferStartTime = DateTime.UtcNow;
                Report(progress, (double)i / totalFiles, $"Uploading {relative}...");

                await _sftp.CreateDirectoryAsync(remoteDir);

                await using var stream = File.OpenRead(filePath);
                _transferBytesTotal = stream.Length;
                var p = new ForwardingProgress(progress,
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
            return TransferResult.Ok($"{totalFiles} files uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            return TransferResult.CancelledResult("Upload cancelled");
        }
        catch (Exception ex)
        {
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
        try
        {
            if (filePaths is not null)
            {
                for (int fi = 0; fi < filePaths.Length; fi++)
                {
                    ct.ThrowIfCancellationRequested();
                    var filePath = filePaths[fi];
                    var fileName = Path.GetFileName(filePath);
                    _transferStartTime = DateTime.UtcNow;
                    Report(progress, (double)fi / totalItems, $"Uploading {fileName}...");

                    var remotePath = targetPath.TrimEnd('\\') + "\\" + fileName;
                    await using var stream = File.OpenRead(filePath);
                    _transferBytesTotal = stream.Length;
                    var p = new ForwardingProgress(progress,
                        f => $"Uploading {fileName}... ({f * 100:F0}%){FormatSpeed(f)}");
                    await _sftp.UploadFileAsync(stream, remotePath, p, ct);

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
                for (int fi = 0; fi < folderPaths.Length; fi++)
                {
                    ct.ThrowIfCancellationRequested();
                    var folderPath = folderPaths[fi];
                    var folderName = Path.GetFileName(folderPath.TrimEnd('\\', '/'));
                    var index = fCount + fi;

                    Report(progress, (double)index / totalItems, $"Scanning {folderName}...");

                    var allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                    var folderRoot = folderPath.TrimEnd('\\', '/');

                    foreach (var filePath in allFiles)
                    {
                        ct.ThrowIfCancellationRequested();
                        var relative = filePath.Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\');
                        var remotePath = targetPath.TrimEnd('\\') + "\\" + folderName + "\\" + relative;
                        var lastSep = remotePath.LastIndexOf('\\');
                        var remoteDir = lastSep > 0 ? remotePath[..lastSep].Replace('\\', '/') : string.Empty;

                        _transferStartTime = DateTime.UtcNow;
                        Report(progress, (double)index / totalItems, $"Uploading {relative}...");
                        await _sftp.CreateDirectoryAsync(remoteDir);

                        await using var stream = File.OpenRead(filePath);
                        _transferBytesTotal = stream.Length;
                        var p = new ForwardingProgress(progress,
                            f => $"Uploading {relative}... ({f * 100:F0}%){FormatSpeed(f)}");
                        await _sftp.UploadFileAsync(stream, remotePath, p, ct);
                    }

                    Report(progress, (double)(index + 1) / totalItems, string.Empty);
                    newEntries.Add(new SftpEntry
                    {
                        Name = folderName, FullPath = targetPath.TrimEnd('\\') + "\\" + folderName,
                        IsDirectory = true, Children = { new SftpEntry { Name = "" } }
                    });
                }
            }

            Report(progress, 1, string.Empty);
            return TransferResult.Ok($"{totalItems} item(s) uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            return TransferResult.CancelledResult("Upload cancelled");
        }
        catch (Exception ex)
        {
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
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            var allFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
            if (allFiles.Length == 0)
                return TransferResult.EmptyResult("Empty ZIP — nothing to upload");

            BeginTransfer();
            var ct = Token;
            var totalFiles = allFiles.Length;
            var folderRoot = tempDir.TrimEnd('\\', '/');

            for (int i = 0; i < totalFiles; i++)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = allFiles[i];
                var relative = filePath.Substring(folderRoot.Length).TrimStart('\\', '/').Replace('/', '\\');
                var remotePath = targetPath.TrimEnd('\\') + "\\" + relative;
                var lastSep = remotePath.LastIndexOf('\\');
                var remoteDir = lastSep > 0 ? remotePath[..lastSep].Replace('\\', '/') : string.Empty;

                _transferStartTime = DateTime.UtcNow;
                Report(progress, (double)i / totalFiles, $"Extracting & uploading {relative}...");

                await _sftp.CreateDirectoryAsync(remoteDir);

                await using var stream = File.OpenRead(filePath);
                _transferBytesTotal = stream.Length;
                var p = new ForwardingProgress(progress,
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
            return TransferResult.Ok($"{totalFiles} files extracted and uploaded", newEntries);
        }
        catch (OperationCanceledException)
        {
            return TransferResult.CancelledResult("ZIP upload cancelled");
        }
        catch (Exception ex)
        {
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

            string? partialPath = null;
            _transferStartTime = DateTime.UtcNow;
            try
            {
                for (int i = 0; i < totalFiles; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var (file, relative) = fileList[i];
                    _transferStartTime = DateTime.UtcNow;
                    _transferBytesTotal = await _sftp.GetFileSizeAsync(file.FullPath);
                    Report(progress, (double)i / totalFiles, $"Downloading {file.Name}...");

                    partialPath = Path.Combine(localDir, relative);
                    var parentDir = Path.GetDirectoryName(partialPath);
                    if (!string.IsNullOrEmpty(parentDir))
                        Directory.CreateDirectory(parentDir);

                    await using var stream = File.Create(partialPath);
                    var p = new ForwardingProgress(progress,
                        f => $"Downloading {file.Name}... ({f * 100:F0}%){FormatSpeed(f)}");
                    await _sftp.DownloadFileAsync(file.FullPath, stream, p, ct);

                    Report(progress, (double)(i + 1) / totalFiles, $"{file.Name} downloaded ({i + 1}/{totalFiles})");
                    partialPath = null;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Multi-file download cancelled");
                if (partialPath is not null && File.Exists(partialPath))
                    File.Delete(partialPath);
                return TransferResult.CancelledResult("Download cancelled");
            }

            return TransferResult.OkDownload($"{totalFiles} files downloaded", totalFiles);
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
            Report(progress, 0, $"Downloading {entry.Name}... (0%)");

            await using var stream = File.Create(savePath);
            var p = new ForwardingProgress(progress,
                f => $"Downloading {entry.Name}... ({f * 100:F0}%){FormatSpeed(f)}");
            _transferBytesTotal = await _sftp.DownloadFileAsync(entry.FullPath, stream, p, ct);

            return TransferResult.OkDownload($"{entry.Name} downloaded", 1);
        }
        catch (OperationCanceledException)
        {
            Logger.Warn($"Download cancelled: {entry.Name}");
            if (File.Exists(savePath))
                File.Delete(savePath);
            return TransferResult.CancelledResult($"{entry.Name} cancelled");
        }
        catch (Exception ex)
        {
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
            Report(progress, 0, "Listing files...");
            var allEntries = await _sftp.RecursiveListAsync(entry.FullPath);
            var files = allEntries.Where(e => !e.IsDirectory).ToList();
            var totalFiles = files.Count;

            if (totalFiles == 0)
                return TransferResult.EmptyResult("Empty folder — nothing to download");

            BeginTransfer();
            var ct = Token;
            localRoot = Path.Combine(localRoot, entry.Name.TrimEnd('\\'));
            Directory.CreateDirectory(localRoot);
            var rootPath = entry.FullPath.TrimEnd('\\');
            string? partialPath = null;

            _transferStartTime = DateTime.UtcNow;
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

                    _transferStartTime = DateTime.UtcNow;
                    _transferBytesTotal = await _sftp.GetFileSizeAsync(file.FullPath);
                    Report(progress, (double)i / totalFiles, $"Downloading {file.Name}...");

                    await using var stream = File.Create(partialPath);
                    var p = new ForwardingProgress(progress,
                        f => $"Downloading {file.Name}... ({f * 100:F0}%){FormatSpeed(f)}");
                    await _sftp.DownloadFileAsync(file.FullPath, stream, p, ct);

                    Report(progress, (double)(i + 1) / totalFiles, string.Empty);
                    partialPath = null;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Folder download cancelled: {entry.Name}");
                if (partialPath is not null && File.Exists(partialPath))
                    File.Delete(partialPath);
                return TransferResult.CancelledResult($"{entry.Name} cancelled");
            }

            return TransferResult.OkDownload($"{totalFiles} files downloaded", totalFiles);
        }
        catch (Exception ex)
        {
            return TransferResult.Fail(ex, $"Folder download failed: {entry.Name}: {ex.Message}");
        }
        finally
        {
            EndTransfer();
        }
    }
}
