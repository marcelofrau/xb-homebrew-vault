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
        Logger.Debug($"ListDirectoryAsync: '{path}' (via shell)");
        return Task.Run(async () =>
        {
            if (_ssh is null || !_ssh.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            // Xbox Dev Mode SFTP doesn't support SSH_FXP_OPENDIR,
            // so we use CMD's dir command instead
            var result = await RunShellCommandAsync($"dir \"{path}\" /a /-c");
            if (!result.Success)
                throw new Exception($"dir failed: {result.Error}");

            return ParseDirOutput(result.Output, path);
        });
    }

    private static List<SftpEntry> ParseDirOutput(string output, string parentPath)
    {
        var entries = new List<SftpEntry>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim('\r', ' ');
            // Skip headers, footers, blank lines, dot entries
            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("Volume", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Directory of", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("File(s)") ||
                trimmed.Contains("Dir(s)") ||
                trimmed == "." || trimmed == ".." ||
                trimmed == ".")
                continue;

            // Skip summary lines in any locale: "<count> <unit>(s) ..." (e.g. "0 arquivo(s) 0 bytes")
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed,
                    @"^\d+\s+\w+\(s\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                continue;

            // Match: flexible date prefix <DIR>|size name
            // Date handles MM/DD/YYYY, DD/MM/YYYY, YYYY-MM-DD, etc.
            var match = System.Text.RegularExpressions.Regex.Match(trimmed,
                @"^(\d{1,4}[/.-]\d{1,2}[/.-]\d{1,4}\s+\d{1,2}:\d{2}(?:\s*[AP]M)?)\s+(<DIR>|\d+)\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var name = match.Groups[3].Value.Trim();
            if (string.IsNullOrEmpty(name) || name == "." || name == "..")
                continue;

            var isDir = match.Groups[2].Value == "<DIR>";
            var size = isDir ? 0 : long.TryParse(match.Groups[2].Value, out var s) ? s : 0;

            var fullPath = parentPath.TrimEnd('\\') + "\\" + name;
            var entry = new SftpEntry
            {
                Name = name,
                FullPath = fullPath,
                IsDirectory = isDir,
                Size = size,
                LastModified = DateTime.MinValue
            };
            if (isDir)
                entry.Children.Add(new SftpEntry { Name = "" });
            entries.Add(entry);
        }

        entries.Sort((a, b) =>
        {
            if (a.IsDirectory != b.IsDirectory)
                return a.IsDirectory ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return entries;
    }

    public Task<List<SftpEntry>> RecursiveListAsync(string path)
    {
        return Task.Run(async () =>
        {
            if (_ssh is null || !_ssh.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            var result = await RunShellCommandAsync($"dir \"{path}\" /s /-c");
            if (!result.Success)
                throw new Exception($"dir /s failed: {result.Error}");

            return ParseDirOutputRecursive(result.Output);
        });
    }

    private static List<SftpEntry> ParseDirOutputRecursive(string output)
    {
        var entries = new List<SftpEntry>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? currentDir = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim('\r', ' ');
            if (string.IsNullOrEmpty(trimmed)) continue;

            var dirMatch = System.Text.RegularExpressions.Regex.Match(trimmed,
                @"^Directory\s+of\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (dirMatch.Success)
            {
                currentDir = dirMatch.Groups[1].Value.Trim();
                continue;
            }

            if (trimmed.StartsWith("Volume", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("File(s)") ||
                trimmed.Contains("Dir(s)") ||
                trimmed == "." || trimmed == "..")
                continue;

            var match = System.Text.RegularExpressions.Regex.Match(trimmed,
                @"^(\S+\s+\S+)\s+(<DIR>|\d+)\s+(.+)$");
            if (!match.Success) continue;

            var name = match.Groups[3].Value.Trim();
            if (string.IsNullOrEmpty(name) || name == "." || name == "..")
                continue;

            var isDir = match.Groups[2].Value == "<DIR>";
            var size = isDir ? 0 : long.TryParse(match.Groups[2].Value, out var s) ? s : 0;

            var fullPath = (currentDir ?? "").TrimEnd('\\') + "\\" + name;
            entries.Add(new SftpEntry
            {
                Name = name,
                FullPath = fullPath,
                IsDirectory = isDir,
                Size = size,
                LastModified = DateTime.MinValue
            });
        }

        return entries;
    }

    public Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress)
    {
        var norm = NormalizePath(remotePath);
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");

            var totalBytes = source.Length;
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
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");

            var fileSize = _sftp.GetAttributes(norm).Size;
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
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");
            _sftp.CreateDirectory(norm);
        });
    }

    public Task RenameAsync(string oldPath, string newPath)
    {
        var oldNorm = NormalizePath(oldPath);
        var newNorm = NormalizePath(newPath);
        return Task.Run(() =>
        {
            if (_sftp is null || !_sftp.IsConnected)
                throw new InvalidOperationException("SFTP not connected");
            _sftp.RenameFile(oldNorm, newNorm);
        });
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
