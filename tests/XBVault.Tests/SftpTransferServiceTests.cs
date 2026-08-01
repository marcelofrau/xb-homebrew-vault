using System.IO.Compression;
using System.Text;
using Renci.SshNet.Common;
using XBVault.Models;

namespace XBVault.Tests;

public class SftpTransferServiceTests : IDisposable
{
    private readonly string _dir;

    public SftpTransferServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Tmp(string name) => Path.Combine(_dir, name);

    private string WriteFile(string name, int kb)
    {
        var path = Tmp(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[kb * 1024]);
        return path;
    }

    private string CreateZip(params (string Name, byte[] Content)[] entries)
    {
        var zipPath = Path.Combine(_dir, $"{Guid.NewGuid():N}.zip");
        using (var fs = File.Create(zipPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                using var es = entry.Open();
                es.Write(content, 0, content.Length);
            }
        }
        return zipPath;
    }

    [Fact]
    public async Task UploadFilesAsync_Success_CreatesRemoteEntries()
    {
        var fake = new FakeSftpService();
        using var svc = new SftpTransferService(fake);
        var a = WriteFile("a.bin", 1);
        var b = WriteFile("b.bin", 2);

        var result = await svc.UploadFilesAsync([a, b], @"Dev", null);

        Assert.True(result.Success);
        Assert.Equal(2, result.NewEntries.Count);
        Assert.True(fake.FileExists(@"Dev\a.bin"));
        Assert.True(fake.FileExists(@"Dev\b.bin"));
    }

    [Fact]
    public async Task UploadFilesAsync_Cancel_DeletesPartialRemote()
    {
        var fake = new FakeSftpService { HoldUpload = true };
        using var svc = new SftpTransferService(fake);
        var a = WriteFile("big.bin", 4096);

        var task = svc.UploadFilesAsync([a], @"Dev", null);
        Assert.True(fake.UploadStarted.Wait(2000));
        svc.CancelTransfer();
        fake.UploadRelease.Set();

        var result = await task;
        Assert.True(result.Cancelled);
        Assert.False(fake.FileExists(@"Dev\big.bin"));
    }

    [Fact]
    public async Task UploadFolderAsync_Empty_ReturnsEmptyResult()
    {
        var fake = new FakeSftpService();
        using var svc = new SftpTransferService(fake);
        var folder = Tmp("emptyfolder");
        Directory.CreateDirectory(folder);

        var result = await svc.UploadFolderAsync(folder, @"Dev", null);

        Assert.True(result.Success);
        Assert.True(result.IsEmptyResult);
    }

    [Fact]
    public async Task UploadFolderAsync_Success_UploadsNestedFiles()
    {
        var fake = new FakeSftpService();
        using var svc = new SftpTransferService(fake);
        var folder = Tmp("game");
        WriteFile(Path.Combine("game", "a.bin"), 1);
        WriteFile(Path.Combine("game", "sub", "b.bin"), 2);

        var result = await svc.UploadFolderAsync(folder, @"Dev", null);

        Assert.True(result.Success);
        Assert.Single(result.NewEntries);
        Assert.True(fake.FileExists(@"Dev\game\a.bin"));
        Assert.True(fake.FileExists(@"Dev\game\sub\b.bin"));
    }

    [Fact]
    public async Task UploadMixedAsync_Success_UploadsFileAndFolder()
    {
        var fake = new FakeSftpService();
        using var svc = new SftpTransferService(fake);
        var file = WriteFile("iso.xex", 1);
        var folder = Tmp("mods");
        Directory.CreateDirectory(folder);
        WriteFile(Path.Combine("mods", "m.bin"), 1);

        var result = await svc.UploadMixedAsync([file], [folder], @"Dev", null);

        Assert.True(result.Success);
        Assert.Equal(2, result.NewEntries.Count);
        Assert.True(fake.FileExists(@"Dev\iso.xex"));
        Assert.True(fake.FileExists(@"Dev\mods\m.bin"));
    }

    [Fact]
    public async Task UploadZipExtractAsync_Success_UploadsAndReportsEntries()
    {
        var fake = new FakeSftpService();
        using var svc = new SftpTransferService(fake);
        var zip = CreateZip(("pkg/a.xex", Encoding.UTF8.GetBytes("a")),
                            ("root.bin", Encoding.UTF8.GetBytes("bb")));

        var result = await svc.UploadZipExtractAsync(zip, @"Dev", null);

        Assert.True(result.Success);
        Assert.Equal(2, result.NewEntries.Count);
        Assert.True(fake.FileExists(@"Dev\pkg\a.xex"));
        Assert.True(fake.FileExists(@"Dev\root.bin"));
    }

    [Fact]
    public async Task UploadZipExtractAsync_EmptyZip_ReturnsEmptyResult()
    {
        var fake = new FakeSftpService();
        using var svc = new SftpTransferService(fake);
        var emptyDir = Tmp("emptyzip");
        Directory.CreateDirectory(emptyDir);
        var zip = Path.Combine(_dir, $"{Guid.NewGuid():N}.zip");
        ZipFile.CreateFromDirectory(emptyDir, zip);

        var result = await svc.UploadZipExtractAsync(zip, @"Dev", null);

        Assert.True(result.Success);
        Assert.True(result.IsEmptyResult);
    }

    [Fact]
    public async Task DownloadFilesAsync_Success_DownloadsFilesAndFolders()
    {
        var fake = new FakeSftpService();
        fake.SeedFile(@"Games\a.bin", Encoding.UTF8.GetBytes("aaa"));
        fake.SeedFile(@"Games\sub\b.bin", Encoding.UTF8.GetBytes("bbbb"));
        using var svc = new SftpTransferService(fake);
        var entries = new List<SftpEntry>
        {
            new() { Name = "a.bin", FullPath = @"Games\a.bin" },
            new() { Name = "sub", FullPath = @"Games\sub", IsDirectory = true }
        };
        var local = Tmp("dl");

        var result = await svc.DownloadFilesAsync(entries, local, null);

        Assert.True(result.Success);
        Assert.Equal(2, result.DownloadedCount);
        Assert.Equal("aaa", File.ReadAllText(Path.Combine(local, "a.bin")));
        Assert.Equal("bbbb", File.ReadAllText(Path.Combine(local, "sub", "b.bin")));
    }

    [Fact]
    public async Task DownloadSingleFileAsync_Success_WritesContent()
    {
        var fake = new FakeSftpService();
        fake.SeedFile(@"Games\a.bin", Encoding.UTF8.GetBytes("hello"));
        using var svc = new SftpTransferService(fake);
        var entry = new SftpEntry { Name = "a.bin", FullPath = @"Games\a.bin" };
        var savePath = Tmp("out.bin");

        var result = await svc.DownloadSingleFileAsync(entry, savePath, null);

        Assert.True(result.Success);
        Assert.Equal(1, result.DownloadedCount);
        Assert.Equal("hello", File.ReadAllText(savePath));
    }

    [Fact]
    public async Task DownloadSingleFileAsync_Cancel_RemovesPartialFile()
    {
        var fake = new FakeSftpService { HoldDownload = true };
        fake.SeedFile(@"Games\big.bin", new byte[4 * 1024 * 1024]);
        using var svc = new SftpTransferService(fake);
        var entry = new SftpEntry { Name = "big.bin", FullPath = @"Games\big.bin" };
        var savePath = Tmp("partial.bin");

        var task = svc.DownloadSingleFileAsync(entry, savePath, null);
        Assert.True(fake.DownloadStarted.Wait(3000), "DownloadStarted not set in 3s");
        svc.CancelTransfer();
        fake.DownloadRelease.Set();

        var result = await task;
        Assert.True(result.Cancelled);
        Assert.False(File.Exists(savePath));
    }

    [Fact]
    public async Task DownloadFolderAsync_Empty_ReturnsEmptyResult()
    {
        var fake = new FakeSftpService();
        fake.SeedDir(@"Games\Empty");
        using var svc = new SftpTransferService(fake);
        var entry = new SftpEntry { Name = "Empty", FullPath = @"Games\Empty", IsDirectory = true };

        var result = await svc.DownloadFolderAsync(entry, Tmp("dl"), null);

        Assert.True(result.Success);
        Assert.True(result.IsEmptyResult);
    }

    [Fact]
    public async Task DownloadFolderAsync_Success_PreservesNestedStructure()
    {
        var fake = new FakeSftpService();
        fake.SeedFile(@"Games\Nested\a.bin", Encoding.UTF8.GetBytes("aaa"));
        fake.SeedFile(@"Games\Nested\sub\b.bin", Encoding.UTF8.GetBytes("bbbb"));
        using var svc = new SftpTransferService(fake);
        var entry = new SftpEntry { Name = "Nested", FullPath = @"Games\Nested", IsDirectory = true };

        var result = await svc.DownloadFolderAsync(entry, Tmp("dl"), null);

        Assert.True(result.Success);
        Assert.Equal(2, result.DownloadedCount);
        Assert.Equal("aaa", File.ReadAllText(Path.Combine(Tmp("dl"), "Nested", "a.bin")));
        Assert.Equal("bbbb", File.ReadAllText(Path.Combine(Tmp("dl"), "Nested", "sub", "b.bin")));
    }

    [Fact]
    public async Task ConnectionLost_ReturnsConnectionLostMessage()
    {
        var fake = new FakeSftpService { ThrowConnectionLost = true };
        using var svc = new SftpTransferService(fake);
        var a = WriteFile("a.bin", 1);

        var result = await svc.UploadFilesAsync([a], @"Dev", null);

        Assert.False(result.Success);
        Assert.Equal("Transfer failed — connection lost", result.StatusMessage);
    }
}

internal sealed class FakeSftpService : ISftpService
{
    public bool IsConnected { get; private set; }
    public event EventHandler<bool>? ConnectionChanged;

    public bool HoldUpload { get; set; }
    public bool HoldDownload { get; set; }
    public bool ThrowConnectionLost { get; set; }
    public ManualResetEventSlim UploadStarted { get; } = new(false);
    public ManualResetEventSlim UploadRelease { get; } = new(false);
    public ManualResetEventSlim DownloadStarted { get; } = new(false);
    public ManualResetEventSlim DownloadRelease { get; } = new(false);

    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');

    public void SeedDir(string path)
    {
        lock (_dirs)
        {
            var key = Normalize(path);
            _dirs.Add(key);
            var parent = Path.GetDirectoryName(key);
            while (!string.IsNullOrEmpty(parent))
            {
                _dirs.Add(parent);
                parent = Path.GetDirectoryName(parent);
            }
        }
    }

    public void SeedFile(string path, byte[] content)
    {
        SeedDir(Path.GetDirectoryName(path)!);
        lock (_dirs)
        {
            _files[Normalize(path)] = content;
        }
    }

    public bool FileExists(string path)
    {
        lock (_dirs) return _files.ContainsKey(Normalize(path));
    }

    public Task ConnectAsync(string host, int port, string user, string pass)
    {
        IsConnected = true;
        ConnectionChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        IsConnected = false;
        ConnectionChanged?.Invoke(this, false);
    }

    public Task<List<SftpEntry>> ListDirectoryAsync(string path)
    {
        var prefix = Normalize(path) + "\\";
        lock (_dirs)
        {
            var dirs = _dirs
                .Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !d[prefix.Length..].Contains('\\'))
                .Select(d => new SftpEntry
                {
                    Name = d[(d.LastIndexOf('\\') + 1)..],
                    FullPath = d,
                    IsDirectory = true,
                    Children = { new SftpEntry { Name = "" } }
                });
            var files = _files
                .Where(f => f.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !f.Key[prefix.Length..].Contains('\\'))
                .Select(f => new SftpEntry
                {
                    Name = Path.GetFileName(f.Key),
                    FullPath = f.Key,
                    Size = f.Value.Length,
                    LastModified = DateTime.UtcNow
                });
            return Task.FromResult(dirs.Concat(files).ToList());
        }
    }

    public Task<List<SftpEntry>> RecursiveListAsync(string path)
    {
        var prefix = Normalize(path) + "\\";
        lock (_dirs)
        {
            return Task.FromResult(_files
                .Where(f => f.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(f => new SftpEntry
                {
                    Name = Path.GetFileName(f.Key),
                    FullPath = f.Key,
                    Size = f.Value.Length,
                    LastModified = DateTime.UtcNow
                })
                .ToList());
        }
    }

    public async Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress, CancellationToken ct = default)
    {
        if (ThrowConnectionLost) throw new SshConnectionException("simulated drop");
        var total = source.Length;
        var buffer = new byte[4096];
        var written = 0L;
        using var ms = new MemoryStream();
        int read;
        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            ms.Write(buffer, 0, read);
            written += read;
            progress?.Report(written / (double)total);
            if (HoldUpload)
            {
                await Task.Yield();
                UploadStarted.Set();
                UploadRelease.Wait();
            }
        }
        var key = Normalize(remotePath);
        lock (_dirs)
        {
            _files[key] = ms.ToArray();
            SeedDir(Path.GetDirectoryName(key)!);
        }
    }

    public Task<long> GetFileSizeAsync(string remotePath)
    {
        lock (_dirs) return Task.FromResult((long)_files[Normalize(remotePath)].Length);
    }

    public async Task<long> DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress, CancellationToken ct = default)
    {
        if (ThrowConnectionLost) throw new SshConnectionException("simulated drop");
        byte[] data;
        lock (_dirs) data = _files[Normalize(remotePath)];
        if (HoldDownload)
        {
            // Yield first so the sync Wait() runs on a pool thread, not the caller's
            await Task.Yield();
            DownloadStarted.Set();
            DownloadRelease.Wait();
        }
        var total = data.Length;
        for (int i = 0; i < total; i += 4096)
        {
            ct.ThrowIfCancellationRequested();
            var n = Math.Min(4096, total - i);
            await destination.WriteAsync(data, i, n);
            progress?.Report((double)(i + n) / total);
        }
        return total;
    }

    public Task DeleteFileAsync(string path)
    {
        lock (_dirs) _files.Remove(Normalize(path));
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path)
    {
        var prefix = Normalize(path) + "\\";
        lock (_dirs)
        {
            foreach (var f in _files.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
                _files.Remove(f);
            foreach (var d in _dirs.Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
                _dirs.Remove(d);
            _dirs.Remove(Normalize(path));
        }
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path)
    {
        SeedDir(path);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string oldPath, string newPath)
    {
        lock (_dirs)
        {
            if (_files.TryGetValue(Normalize(oldPath), out var data))
            {
                _files.Remove(Normalize(oldPath));
                _files[Normalize(newPath)] = data;
            }
        }
        return Task.CompletedTask;
    }

    public Task<SftpShellResult> RunShellCommandAsync(string command) =>
        Task.FromResult(new SftpShellResult { Success = true });

    public void Dispose()
    {
    }
}
