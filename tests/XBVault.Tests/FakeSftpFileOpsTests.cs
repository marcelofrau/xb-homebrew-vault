using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XBVault.Models;
using Xunit;

namespace XBVault.Tests;

public class FakeSftpFileOpsTests
{
    [Fact]
    public async Task DeleteDirectory_RemovesContents()
    {
        var fake = new FakeSftpService();
        fake.SeedFile(@"Games\a.bin", Encoding.UTF8.GetBytes("a"));
        fake.SeedFile(@"Games\sub\b.bin", Encoding.UTF8.GetBytes("b"));

        await fake.DeleteDirectoryAsync("Games");

        Assert.False(fake.FileExists(@"Games\a.bin"));
        Assert.False(fake.FileExists(@"Games\sub\b.bin"));
    }

    [Fact]
    public async Task RenameFile_MovesFile()
    {
        var fake = new FakeSftpService();
        fake.SeedFile(@"Games\old.bin", Encoding.UTF8.GetBytes("x"));

        await fake.RenameAsync(@"Games\old.bin", @"Games\new.bin");

        Assert.False(fake.FileExists(@"Games\old.bin"));
        Assert.True(fake.FileExists(@"Games\new.bin"));
    }

    [Fact]
    public async Task ListDirectory_ReturnsDirectoriesAndFiles()
    {
        var fake = new FakeSftpService();
        fake.SeedDir(@"Games\dir1");
        fake.SeedFile(@"Games\file1.bin", Encoding.UTF8.GetBytes("f"));

        var list = await fake.ListDirectoryAsync("Games");

        Assert.Contains(list, e => e.IsDirectory && e.Name == "dir1");
        Assert.Contains(list, e => !e.IsDirectory && e.Name == "file1.bin");
    }

    [Fact]
    public async Task RecursiveList_ReturnsAllFilesUnderPrefix()
    {
        var fake = new FakeSftpService();
        fake.SeedFile(@"Games\root.bin", Encoding.UTF8.GetBytes("r"));
        fake.SeedFile(@"Games\nested\a.bin", Encoding.UTF8.GetBytes("a"));

        var all = await fake.RecursiveListAsync("Games");

        Assert.Contains(all, e => e.FullPath.EndsWith("root.bin"));
        Assert.Contains(all, e => e.FullPath.EndsWith("nested\\a.bin"));
    }

    [Fact]
    public void Normalize_Handles_ForwardSlashes_And_TrailingBackslash()
    {
        var input = "Games/dir/file.bin/";
        var norm = FakeSftpService.Normalize(input);
        Assert.Equal("Games\\dir\\file.bin", norm);
    }
}
