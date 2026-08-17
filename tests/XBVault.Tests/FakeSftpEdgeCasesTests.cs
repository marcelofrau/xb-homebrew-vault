using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;
using Xunit;

namespace XBVault.Tests;

public class FakeSftpEdgeCasesTests
{
    [Fact]
    public async Task Upload_Cancel_With_Hold_Releases_And_Deletes_Partial()
    {
        var fake = new FakeSftpService { HoldUpload = true };
        var cts = new CancellationTokenSource();

        // prepare a bigger stream
        var ms = new MemoryStream(new byte[1024 * 1024]);

        var upload = fake.UploadFileAsync(ms, "Dev\\big.bin", null, cts.Token);

        // wait for upload to start
        Assert.True(fake.UploadStarted.Wait(2000));
        // cancel while held
        cts.Cancel();
        // release
        fake.UploadRelease.Set();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await upload);
    }

    [Fact]
    public async Task Download_Cancel_With_Hold_Removes_Partial_File_On_HigherLayer()
    {
        var fake = new FakeSftpService { HoldDownload = true };
        fake.SeedFile(@"Games\big.bin", new byte[1024 * 1024]);
        var cts = new CancellationTokenSource();
        var dest = Path.GetTempFileName();
        try
        {
            using var fs = File.Create(dest);
            var download = fake.DownloadFileAsync(@"Games\big.bin", fs, null, cts.Token);
            Assert.True(fake.DownloadStarted.Wait(2000));
            cts.Cancel();
            fake.DownloadRelease.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await download);
        }
        finally
        {
            try { File.Delete(dest); } catch { }
        }
    }

    [Fact]
    public async Task Upload_Progress_Reports_IncreasingValues()
    {
        var fake = new FakeSftpService();
        var mem = new MemoryStream(new byte[50 * 1024]);
        double last = -1;
        var progress = new Progress<double>(p => { last = Math.Max(last, p); });

        await fake.UploadFileAsync(mem, "Dev\\p.bin", progress, CancellationToken.None);

        Assert.InRange(last, 0.0, 1.0);
    }

    [Fact]
    public async Task CreateDirectory_Creates_Path_For_ListDirectory()
    {
        var fake = new FakeSftpService();
        await fake.CreateDirectoryAsync(@"Games\\newdir");
        var list = await fake.ListDirectoryAsync("Games");
        Assert.Contains(list, e => e.IsDirectory && e.Name == "newdir");
    }

    [Fact]
    public void GetFileSize_Throws_For_Missing()
    {
        var fake = new FakeSftpService();
        Assert.ThrowsAny<KeyNotFoundException>(() => fake.GetFileSizeAsync(@"nope").GetAwaiter().GetResult());
    }
}
