using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public interface ISftpService : IDisposable
{
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionChanged;

    Task ConnectAsync(string host, int port, string user, string pass);
    void Disconnect();
    Task<List<SftpEntry>> ListDirectoryAsync(string path);
    Task<List<SftpEntry>> RecursiveListAsync(string path);
    Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress, CancellationToken ct = default);
    Task<long> GetFileSizeAsync(string remotePath);
    Task<long> DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress, CancellationToken ct = default);
    Task DeleteFileAsync(string path);
    Task DeleteDirectoryAsync(string path);
    Task CreateDirectoryAsync(string path);
    Task RenameAsync(string oldPath, string newPath);
    Task<SftpShellResult> RunShellCommandAsync(string command);
}
