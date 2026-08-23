#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

/// <summary>
/// Defines low-level SSH/SFTP filesystem operations used by the file explorer and transfer services.
/// </summary>
/// <remarks>
/// Implementations should keep this contract platform-neutral. Higher-level workflows belong in
/// <see cref="SftpTransferService"/> or ViewModels.
/// </remarks>
public interface ISftpService : IDisposable
{
    /// <summary>
    /// Gets whether the underlying SSH/SFTP session is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    event EventHandler<bool>? ConnectionChanged;

    /// <summary>
    /// Opens an SSH/SFTP connection.
    /// </summary>
    Task ConnectAsync(string host, int port, string user, string pass);

    /// <summary>
    /// Closes any active SSH/SFTP connection.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Lists one directory level at the specified remote path.
    /// </summary>
    Task<List<SftpEntry>> ListDirectoryAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Lists all files below a remote path recursively.
    /// </summary>
    Task<List<SftpEntry>> RecursiveListAsync(string path);

    /// <summary>
    /// Uploads a stream to a remote path.
    /// </summary>
    Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress, CancellationToken ct = default);

    /// <summary>
    /// Returns remote file size in bytes.
    /// </summary>
    Task<long> GetFileSizeAsync(string remotePath);

    /// <summary>
    /// Downloads a remote file into a destination stream.
    /// </summary>
    Task<long> DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress, CancellationToken ct = default);

    /// <summary>
    /// Deletes a remote file.
    /// </summary>
    Task DeleteFileAsync(string path);

    /// <summary>
    /// Deletes a remote directory.
    /// </summary>
    Task DeleteDirectoryAsync(string path);

    /// <summary>
    /// Creates a remote directory.
    /// </summary>
    Task CreateDirectoryAsync(string path);

    /// <summary>
    /// Renames or moves a remote file or directory.
    /// </summary>
    Task RenameAsync(string oldPath, string newPath);

    /// <summary>
    /// Runs a shell command over SSH and returns output, exit code, and error text.
    /// </summary>
    Task<SftpShellResult> RunShellCommandAsync(string command, CancellationToken ct = default);
}
