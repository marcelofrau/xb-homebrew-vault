using Renci.SshNet;
using XBVault.Models;

namespace XBVault.Services;

public class SftpService : IDisposable
{
    private SshClient? _ssh;
    private SftpClient? _sftp;

    public bool IsConnected => _sftp?.IsConnected ?? false;

    public event EventHandler<bool>? ConnectionChanged;

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

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
        if (_sftp?.IsConnected == true)
        {
            try { _sftp.Disconnect(); } catch { }
        }
        if (_ssh?.IsConnected == true)
        {
            try { _ssh.Disconnect(); } catch { }
        }

        _sftp?.Dispose();
        _sftp = null;
        _ssh?.Dispose();
        _ssh = null;

        ConnectionChanged?.Invoke(this, false);
        Logger.Debug("SFTP disconnected");
    }

    public Task<List<SftpEntry>> ListDirectoryAsync(string path)
    {
        Logger.Debug($"ListDirectoryAsync: '{path}' (via shell dir /b)");
        return Task.Run(async () =>
        {
            if (_ssh is null || !_ssh.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            // Use dir /b (bare) — just names, no headers/metadata to parse
            var parent = path.TrimEnd('\\');

            // Get directories
            var dirResult = await RunShellCommandAsync($"dir \"{path}\" /b /ad");
            var dirNames = dirResult.Success
                ? dirResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim('\r', ' ', '\t'))
                    .Where(n => n.Length > 0)
                : Array.Empty<string>();

            // Get files
            var fileResult = await RunShellCommandAsync($"dir \"{path}\" /b /a-d");
            var fileNames = fileResult.Success
                ? fileResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim('\r', ' ', '\t'))
                    .Where(n => n.Length > 0)
                : Array.Empty<string>();

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
                    Size = 0,
                    LastModified = DateTime.MinValue
                });
            }

            entries.Sort((a, b) =>
            {
                if (a.IsDirectory != b.IsDirectory)
                    return a.IsDirectory ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return entries;
        });
    }

    public Task<List<SftpEntry>> RecursiveListAsync(string path)
    {
        return Task.Run(async () =>
        {
            if (_ssh is null || !_ssh.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            // dir /s /b /a-d: all file paths recursively, bare format
            var result = await RunShellCommandAsync($"dir \"{path}\" /s /b /a-d");
            if (!result.Success)
                throw new Exception($"dir /s /b /a-d failed: {result.Error}");

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

            return entries;
        });
    }

    public Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress)
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

            using var remoteStream = _sftp.OpenWrite(norm);

            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                remoteStream.Write(buffer, 0, bytesRead);
                uploadedBytes += bytesRead;
                progress?.Report((double)uploadedBytes / totalBytes);
            }
        });
    }

    public Task DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress)
    {
        var norm = NormalizePath(remotePath);
        Logger.Debug($"DownloadFileAsync: '{norm}'");
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");

            var fileSize = _sftp.GetAttributes(norm).Size;
            Logger.Debug($"DownloadFileAsync: '{norm}' size={fileSize}");
            long downloadedBytes = 0;
            var buffer = new byte[32768];
            var bytesRead = 0;

            using var remoteStream = _sftp.OpenRead(norm);

            while ((bytesRead = remoteStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;
                progress?.Report((double)downloadedBytes / fileSize);
            }
        });
    }

    public Task DeleteFileAsync(string path)
    {
        var norm = NormalizePath(path);
        Logger.Debug($"DeleteFileAsync: '{norm}'");
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");
            _sftp.DeleteFile(norm);
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
            _sftp.DeleteDirectory(norm);
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
