using XBVault.Views;

namespace XBVault.Tests;

public class MobileErrorDialogViewModelTests
{
    [Fact]
    public void HasDownload_False_WhenDownloadUrlNull()
    {
        var vm = new MobileErrorDialogViewModel();
        Assert.False(vm.HasDownload);
    }

    [Fact]
    public void HasDownload_False_WhenDownloadUrlEmpty()
    {
        var vm = new MobileErrorDialogViewModel { DownloadUrl = "" };
        Assert.False(vm.HasDownload);
    }

    [Fact]
    public void HasDownload_True_WhenDownloadUrlSet()
    {
        var vm = new MobileErrorDialogViewModel { DownloadUrl = "https://github.com/x/releases" };
        Assert.True(vm.HasDownload);
    }

    [Fact]
    public void HasDetails_False_WhenDetailsNull()
    {
        var vm = new MobileErrorDialogViewModel();
        Assert.False(vm.HasDetails);
    }

    [Fact]
    public void HasDetails_True_WhenDetailsSet()
    {
        var vm = new MobileErrorDialogViewModel { Details = "some detail" };
        Assert.True(vm.HasDetails);
    }
}
