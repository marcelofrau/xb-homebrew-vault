using XBVault.Models;

namespace XBVault.Tests;

public class SftpEntryTests
{
    // ---- FormattedSize ----

    [Theory]
    [InlineData(0, "0.0B")]
    [InlineData(500, "500.0B")]
    [InlineData(1024, "1.0KB")]
    [InlineData(1536, "1.5KB")]
    [InlineData(1024 * 1024, "1.0MB")]
    [InlineData(3L * 1024 * 1024, "3.0MB")]
    [InlineData(1024L * 1024 * 1024, "1.0GB")]
    [InlineData(2L * 1024 * 1024 * 1024, "2.0GB")]
    public void FormattedSize_FormatsCorrectly(long bytes, string expected)
    {
        var entry = new SftpEntry { Size = bytes, IsDirectory = false };
        Assert.Equal(expected, entry.FormattedSize);
    }

    [Fact]
    public void FormattedSize_Directory_ReturnsEmpty()
    {
        var entry = new SftpEntry { IsDirectory = true, Size = 99999 };
        Assert.Equal("", entry.FormattedSize);
    }

    // ---- Children / HeaderMargin ----

    [Fact]
    public void HeaderMargin_DriveEntry_ReturnsZero()
    {
        var entry = new SftpEntry { IsDrive = true };
        Assert.Equal(new Avalonia.Thickness(0), entry.HeaderMargin);
    }

    [Fact]
    public void HeaderMargin_PlaceholderEntry_ReturnsZero()
    {
        var entry = new SftpEntry { IsPlaceholder = true };
        Assert.Equal(new Avalonia.Thickness(0), entry.HeaderMargin);
    }

    [Fact]
    public void HeaderMargin_NonDriveNonPlaceholderNoChildren_ReturnsIndented()
    {
        var entry = new SftpEntry { IsDrive = false, IsPlaceholder = false };
        Assert.Equal(new Avalonia.Thickness(23, 0, 0, 0), entry.HeaderMargin);
    }

    [Fact]
    public void HeaderMargin_NonDriveWithChildren_ReturnsZero()
    {
        var entry = new SftpEntry { IsDrive = false, IsPlaceholder = false };
        entry.Children.Add(new SftpEntry { Name = "child" });
        Assert.Equal(new Avalonia.Thickness(0), entry.HeaderMargin);
    }

    [Fact]
    public void Children_CollectionChanged_UpdatesHeaderMargin()
    {
        var entry = new SftpEntry { IsDrive = false, IsPlaceholder = false };
        var notified = false;
        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "HeaderMargin") notified = true;
        };

        entry.Children.Add(new SftpEntry { Name = "child" });

        Assert.True(notified);
    }

    // ---- IsSelected / IsLastChild / IsExpanded ----

    [Fact]
    public void IsSelected_SetSameValue_NoNotification()
    {
        var entry = new SftpEntry { IsSelected = true };
        var notified = false;
        entry.PropertyChanged += (_, _) => notified = true;

        entry.IsSelected = true;

        Assert.False(notified);
    }

    [Fact]
    public void IsLastChild_SetDifferentValue_RaisesNotification()
    {
        var entry = new SftpEntry();
        var notified = false;
        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "IsLastChild") notified = true;
        };

        entry.IsLastChild = true;

        Assert.True(notified);
    }

    [Fact]
    public void IsExpanded_SetDifferentValue_RaisesNotification()
    {
        var entry = new SftpEntry();
        var notified = false;
        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "IsExpanded") notified = true;
        };

        entry.IsExpanded = true;

        Assert.True(notified);
    }

    // ---- IconPath static configuration ----

    [Fact]
    public void IconViewFolder_DefaultIsFileExplorerView()
    {
        Assert.Equal("FileExplorerView", SftpEntry.IconViewFolder);
    }

    [Fact]
    public void IconSizeSuffix_DefaultIs24()
    {
        Assert.Equal("24", SftpEntry.IconSizeSuffix);
    }

    [Fact]
    public void IconFilePrefix_DefaultIsFileexplorer()
    {
        Assert.Equal("fileexplorer", SftpEntry.IconFilePrefix);
    }
}
