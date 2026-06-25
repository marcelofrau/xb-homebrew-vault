using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using XBVault.Models;
using System.Text.RegularExpressions;

namespace XBVault.Services;

public class SftpService : IDisposable
{
    private SshClient? _ssh;
    private SftpClient? _sftp;

    public bool IsConnected => _sftp?.IsConnected ?? false;

    public event EventHandler<bool>? ConnectionChanged;

    private static string NormalizePath(string path)
    {
        path = path.Replace('\\', '/');
        if (path.Length >= 2 && path[1] == ':' && !path.StartsWith('/'))
            path = "/" + path;
        return path;
    }

    private static string ShellPath(string path) =>
        path.Replace('/', '\\');

    public async Task ConnectAsync(string host, int port, string user, string pass)
    {
        Logger.Debug($"SftpService.ConnectAsync: connecting to {host}:{port} as {user}");
        await Task.Run(() =>
        {
            try
            {
                Logger.Debug("Creating SSH connection info...");
                var connInfo = new ConnectionInfo(host, port, user,
                    new PasswordAuthenticationMethod(user, pass));

                Logger.Debug("Connecting SSH client...");
                _ssh = new SshClient(connInfo);
                _ssh.Connect();
                Logger.Debug($"SSH client connected: {_ssh.IsConnected}");

                Logger.Debug("Connecting SFTP client...");
                _sftp = new SftpClient(connInfo);
                _sftp.OperationTimeout = TimeSpan.FromSeconds(15);
                _sftp.KeepAliveInterval = TimeSpan.FromSeconds(30);
                _sftp.Connect();
                Logger.Debug($"SFTP client connected: {_sftp.IsConnected}");

                Logger.Info($"SFTP connection established to {host}:{port} as {user}");
                ConnectionChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"SFTP connection failed to {host}:{port} as {user}");
                Disconnect();
                throw;
            }
        });
    }

    public void Disconnect()
    {
        Logger.Debug("SftpService.Disconnect: starting...");
        if (_sftp?.IsConnected == true)
        {
            try { _sftp.Disconnect(); Logger.Trace("SFTP client disconnected"); } catch { }
        }
        if (_ssh?.IsConnected == true)
        {
            try { _ssh.Disconnect(); Logger.Trace("SSH client disconnected"); } catch { }
        }

        _sftp?.Dispose();
        _sftp = null;
        _ssh?.Dispose();
        _ssh = null;

        ConnectionChanged?.Invoke(this, false);
        Logger.Debug("SftpService.Disconnect: complete");
    }

    public Task<List<SftpEntry>> ListDirectoryAsync(string path)
    {
        Logger.Debug($"ListDirectoryAsync: '{path}' (via shell dir /b)");
        return Task.Run(async () =>
        {
            if (_ssh is null || !_ssh.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            var parent = path.TrimEnd('\\');

            var dirResult = await RunShellCommandAsync($"dir \"{path}\" /b /ad");
            var dirNames = dirResult.Success
                ? dirResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim('\r', ' ', '\t'))
                    .Where(n => n.Length > 0)
                : Array.Empty<string>();

            var fileResult = await RunShellCommandAsync($"dir \"{path}\" /b /a-d");
            var fileNames = fileResult.Success
                ? fileResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim('\r', ' ', '\t'))
                    .Where(n => n.Length > 0)
                : Array.Empty<string>();

            // dir /a-d (verbose) to extract file sizes
            var sizeResult = await RunShellCommandAsync($"dir \"{path}\" /a-d");
            var sizeByName = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (sizeResult.Success)
            {
                foreach (var line in sizeResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim('\r');
                    var match = Regex.Match(trimmed, @"^\s*\d+[/-]\d+[/-]\d+\s+\d+:\d+\s+(?:AM|PM)?\s*([\d,.\s]+)\s+(.+)$", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var clean = Regex.Replace(match.Groups[1].Value, @"[^\d]", "");
                        if (long.TryParse(clean, out var sz))
                        {
                            var name = match.Groups[2].Value.Trim();
                            sizeByName[name] = sz;
                        }
                    }
                }
            }

            var entries = new List<SftpEntry>(dirNames.Count() + fileNames.Count());

            foreach (var name in dirNames)
            {
                var fullPath = parent + "\\" + name;
                entries.Add(new SftpEntry
                {
                    Name = name,
                    FullPath = fullPath,
                    IsDirectory = true,
                    Children = { new SftpEntry { Name = "" } }
                });
            }

            foreach (var name in fileNames)
            {
                var fullPath = parent + "\\" + name;
                entries.Add(new SftpEntry
                {
                    Name = name,
                    FullPath = fullPath,
                    IsDirectory = false,
                    Size = sizeByName.TryGetValue(name, out var s) ? s : 0,
                    LastModified = DateTime.MinValue
                });
            }

            entries.Sort((a, b) =>
            {
                if (a.IsDirectory != b.IsDirectory)
                    return a.IsDirectory ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            Logger.Debug($"ListDirectoryAsync: '{path}' returned {entries.Count} entries");
            return entries;
        });
    }

    public Task<List<SftpEntry>> RecursiveListAsync(string path)
    {
        Logger.Trace($"RecursiveListAsync: '{path}'");
        return Task.Run(async () =>
        {
            if (_ssh is null || !_ssh.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            // dir /s /b /a-d: all file paths recursively, bare format
            var result = await RunShellCommandAsync($"dir \"{path}\" /s /b /a-d");
            if (!result.Success)
            {
                var err = result.Error ?? "";
                if (err.Contains("não encontrado", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("no files", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Debug($"RecursiveListAsync: '{path}' is empty (exit=1, err=empty dir)");
                    return [];
                }
                throw new Exception($"dir /s /b /a-d failed: {result.Error}");
            }

            var parent = path.TrimEnd('\\');
            var entries = new List<SftpEntry>();
            var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var fullPath = line.Trim('\r', ' ', '\t');
                if (fullPath.Length == 0) continue;

                entries.Add(new SftpEntry
                {
                    Name = Path.GetFileName(fullPath),
                    FullPath = fullPath,
                    IsDirectory = false
                });

                // Derive parent directories from file paths
                var dir = Path.GetDirectoryName(fullPath);
                while (dir != null && dir.Length > parent.Length && seenDirs.Add(dir))
                {
                    entries.Add(new SftpEntry
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsDirectory = true
                    });
                    dir = Path.GetDirectoryName(dir);
                }
            }

            Logger.Trace($"RecursiveListAsync: '{path}' — {entries.Count} entries");
            return entries;
        });
    }

    public Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress, CancellationToken ct = default)
    {
        var norm = NormalizePath(remotePath);
        Logger.Debug($"UploadFileAsync: -> '{norm}'");
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");

            var totalBytes = source.Length;
            Logger.Debug($"UploadFileAsync: '{norm}' size={totalBytes}");
            long uploadedBytes = 0;
            var buffer = new byte[32768];
            var bytesRead = 0;
            var chunkCount = 0;

            using var remoteStream = _sftp.OpenWrite(norm);

            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                remoteStream.Write(buffer, 0, bytesRead);
                uploadedBytes += bytesRead;
                chunkCount++;
                progress?.Report((double)uploadedBytes / totalBytes);
            }

            Logger.Debug($"UploadFileAsync: '{norm}' done — {chunkCount} chunks, {uploadedBytes}B");
        }, ct);
    }

    public Task<long> DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress, CancellationToken ct = default)
    {
        var norm = NormalizePath(remotePath);
        Logger.Debug($"DownloadFileAsync: '{norm}'");
        return Task.Run<long>(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");

            long fileSize = -1;
            try
            {
                fileSize = _sftp.GetAttributes(norm).Size;
                Logger.Debug($"DownloadFileAsync: GetAttributes OK '{norm}' size={fileSize}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"DownloadFileAsync: GetAttributes failed for '{norm}': {ex.Message}");
            }

            // Try forward-slash first; fall back to backslash
            string usePath = norm;
            SftpFileStream remoteStream;
            try
            {
                Logger.Debug($"DownloadFileAsync: OpenRead '{usePath}'");
                remoteStream = _sftp.OpenRead(usePath);
            }
            catch (SftpPathNotFoundException)
            {
                usePath = ShellPath(remotePath);
                Logger.Warn($"DownloadFileAsync: OpenRead forward-slash failed, trying backslash '{usePath}'");
                remoteStream = _sftp.OpenRead(usePath);
            }

            Logger.Debug($"DownloadFileAsync: '{usePath}' opened OK, size={fileSize}");
            long downloadedBytes = 0;
            var buffer = new byte[32768];
            int bytesRead;
            var chunkCount = 0;

            using (remoteStream)
            {
                Logger.Trace($"DownloadFileAsync: '{usePath}' starting read loop");
                while ((bytesRead = remoteStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    destination.Write(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    chunkCount++;
                    if (fileSize > 0)
                        progress?.Report((double)downloadedBytes / fileSize);
                    if (chunkCount % 100 == 0)
                        Logger.Trace($"DownloadFileAsync: '{usePath}' chunk#{chunkCount} total={downloadedBytes}B");
                }
            }

            Logger.Debug($"DownloadFileAsync: '{usePath}' done — {chunkCount} chunks, {downloadedBytes}B");
            return fileSize;
        }, ct);
    }

    public Task DeleteFileAsync(string path)
    {
        var norm = NormalizePath(path);
        Logger.Debug($"DeleteFileAsync: '{norm}'");
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");
            try
            {
                _sftp.DeleteFile(norm);
            }
            catch (Exception ex)
            {
                Logger.Warn($"DeleteFileAsync: SFTP rm failed: {ex.Message}, trying shell fallback");
                var winPath = ShellPath(path);
                var shellResult = ShellExec($"del /f /q \"{winPath}\"");
                if (!shellResult.Success)
                    throw new InvalidOperationException($"del failed: {shellResult.Error ?? ex.Message}");
            }
        });
    }

    public Task DeleteDirectoryAsync(string path)
    {
        var norm = NormalizePath(path);
        Logger.Debug($"DeleteDirectoryAsync: '{norm}'");
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");
            try
            {
                _sftp.DeleteDirectory(norm);
                Logger.Debug($"DeleteDirectoryAsync: SFTP rmdir OK '{norm}'");
            }
            catch (Exception ex)
            {
                Logger.Warn($"DeleteDirectoryAsync: SFTP rmdir failed: {ex.Message}, trying shell fallback");
                var winPath = ShellPath(path);
                Logger.Debug($"DeleteDirectoryAsync: shell rmdir /s /q \"{winPath}\"");
                var shellResult = ShellExec($"rmdir /s /q \"{winPath}\"");
                if (shellResult.Success)
                    Logger.Debug($"DeleteDirectoryAsync: shell rmdir OK '{norm}'");
                else
                    throw new InvalidOperationException($"rmdir failed: {shellResult.Error ?? ex.Message}");
            }
        });
    }

    public Task CreateDirectoryAsync(string path)
    {
        var norm = NormalizePath(path);
        Logger.Debug($"CreateDirectoryAsync: '{norm}'");
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");
            try
            {
                _sftp.CreateDirectory(norm);
            }
            catch (Exception ex)
            {
                Logger.Warn($"SFTP mkdir failed: {ex.Message}, trying shell fallback");
                var winPath = ShellPath(path);
                var shellResult = ShellExec($"if not exist \"{winPath}\" mkdir \"{winPath}\"");
                if (!shellResult.Success)
                {
                    var err = shellResult.Error ?? ex.Message;
                    if (err.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                        err.Contains("Já existe", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Warn($"Directory already exists (expected): '{path}'");
                        return;
                    }
                    throw new InvalidOperationException($"mkdir failed: {err}");
                }
            }
        });
    }

    public Task RenameAsync(string oldPath, string newPath)
    {
        var oldNorm = NormalizePath(oldPath);
        var newNorm = NormalizePath(newPath);
        Logger.Debug($"RenameAsync: '{oldNorm}' -> '{newNorm}'");
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");
            try
            {
                _sftp.RenameFile(oldNorm, newNorm);
            }
            catch (Exception ex)
            {
                Logger.Warn($"SFTP rename failed: {ex.Message}, trying shell fallback");
                var winOld = ShellPath(oldPath);
                var winNew = ShellPath(newPath);
                var shellResult = ShellExec($"rename \"{winOld}\" \"{Path.GetFileName(newPath)}\"");
                if (!shellResult.Success)
                    throw;
            }
        });
    }

    private SftpShellResult ShellExec(string command)
    {
        if (_ssh is null || !_ssh.IsConnected)
            return new SftpShellResult { Success = false, Error = "SSH not connected" };
        try
        {
            using var cmd = _ssh.RunCommand(command);
            Logger.Debug($"ShellExec: '{command}' exit: {cmd.ExitStatus}");
            if (!string.IsNullOrEmpty(cmd.Error))
                Logger.Warn($"ShellExec stderr: {cmd.Error}");
            return new SftpShellResult
            {
                Success = cmd.ExitStatus == 0,
                Output = cmd.Result ?? string.Empty,
                Error = cmd.Error
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"ShellExec failed: '{command}'");
            return new SftpShellResult { Success = false, Error = ex.Message };
        }
    }

    public Task<SftpShellResult> RunShellCommandAsync(string command)
    {
        return Task.Run(() =>
        {
            if (_ssh is null || !_ssh.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            var result = new SftpShellResult();

            try
            {
                using var cmd = _ssh.RunCommand(command);
                result.Output = cmd.Result ?? string.Empty;
                result.Error = cmd.Error;
                result.Success = cmd.ExitStatus == 0;

                Logger.Debug($"SFTP shell command exit: {cmd.ExitStatus}");
                if (!string.IsNullOrEmpty(cmd.Error))
                    Logger.Warn($"SFTP shell command stderr: {cmd.Error}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"SFTP shell command failed: {command}");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        });
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
