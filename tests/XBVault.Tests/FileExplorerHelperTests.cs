using System.Collections.ObjectModel;
using XBVault.Models;
using XBVault.ViewModels;

namespace XBVault.Tests;

public class FileExplorerHelperTests
{
    private static SftpEntry Dir(string name, string path = "")
    {
        return new SftpEntry { Name = name, FullPath = path.Length > 0 ? path : name, IsDirectory = true };
    }

    private static SftpEntry File(string name, string path = "")
    {
        return new SftpEntry { Name = name, FullPath = path.Length > 0 ? path : name, IsDirectory = false };
    }

    // ---- FormatBps ----

    [Theory]
    [InlineData(0, " 0 B/s")]
    [InlineData(500, " 500 B/s")]
    [InlineData(1024, " 1.0 KB/s")]
    [InlineData(1536, " 1.5 KB/s")]
    [InlineData(1024 * 1024, " 1.0 MB/s")]
    [InlineData(3 * 1024 * 1024, " 3.0 MB/s")]
    public void FormatBps_FormatsUnits(double bps, string expected)
    {
        Assert.Equal(expected, FileExplorerViewModel.FormatBps(bps));
    }

    // ---- InsertSorted ----

    [Fact]
    public void InsertSorted_DirectoriesFirst_ThenFiles()
    {
        var list = new ObservableCollection<SftpEntry>();

        FileExplorerViewModel.InsertSorted(list, File("b.txt"));
        FileExplorerViewModel.InsertSorted(list, Dir("a-folder"));

        Assert.Equal(2, list.Count);
        Assert.True(list[0].IsDirectory);
        Assert.False(list[1].IsDirectory);
    }

    [Fact]
    public void InsertSorted_Alphabetical_WithinKind()
    {
        var list = new ObservableCollection<SftpEntry>();

        FileExplorerViewModel.InsertSorted(list, File("zeta.txt"));
        FileExplorerViewModel.InsertSorted(list, File("alpha.txt"));
        FileExplorerViewModel.InsertSorted(list, File("mid.txt"));

        Assert.Equal(new[] { "alpha.txt", "mid.txt", "zeta.txt" }, list.Select(e => e.Name).ToArray());
    }

    [Fact]
    public void InsertSorted_CaseInsensitiveOrder()
    {
        var list = new ObservableCollection<SftpEntry>();

        FileExplorerViewModel.InsertSorted(list, File("Beta.txt"));
        FileExplorerViewModel.InsertSorted(list, File("alpha.txt"));

        Assert.Equal(new[] { "alpha.txt", "Beta.txt" }, list.Select(e => e.Name).ToArray());
    }

    [Fact]
    public void InsertSorted_PlaceholderStaysFirst()
    {
        var list = new ObservableCollection<SftpEntry> { new() { Name = "", IsPlaceholder = true } };

        FileExplorerViewModel.InsertSorted(list, File("a.txt"));

        Assert.True(list[0].IsPlaceholder);
        Assert.Equal("a.txt", list[1].Name);
    }

    [Fact]
    public void InsertSorted_MarksLastChild()
    {
        var list = new ObservableCollection<SftpEntry>();
        FileExplorerViewModel.InsertSorted(list, File("a.txt"));
        FileExplorerViewModel.InsertSorted(list, File("b.txt"));

        Assert.False(list[0].IsLastChild);
        Assert.True(list[1].IsLastChild);
    }

    // ---- UpdateLastChildFlag ----

    [Fact]
    public void UpdateLastChildFlag_OnlyLastIsTrue()
    {
        var list = new ObservableCollection<SftpEntry> { File("a"), File("b"), File("c") };

        FileExplorerViewModel.UpdateLastChildFlag(list);

        Assert.Equal(new[] { false, false, true }, list.Select(e => e.IsLastChild).ToArray());
    }

    [Fact]
    public void UpdateLastChildFlag_EmptyList_NoThrow()
    {
        FileExplorerViewModel.UpdateLastChildFlag(new ObservableCollection<SftpEntry>());
    }

    // ---- UpdateChildrenPathsRecursive ----

    [Fact]
    public void UpdateChildrenPathsRecursive_RewritesNestedPaths()
    {
        var oldPath = @"\home\user\games\OldName";
        var newPath = @"\home\user\games\NewName";
        var root = Dir("NewName", newPath);
        var child = Dir("sub", oldPath + @"\sub");
        child.Children.Add(File("file.txt", oldPath + @"\sub\file.txt"));
        root.Children.Add(child);

        FileExplorerViewModel.UpdateChildrenPathsRecursive(root, oldPath, newPath);

        Assert.Equal(newPath + @"\sub", child.FullPath);
        Assert.Equal(newPath + @"\sub\file.txt", child.Children[0].FullPath);
    }

    // ---- FindEntry ----

    [Fact]
    public void FindEntry_FindsNestedEntry_ByPath()
    {
        var root = Dir("games", @"\games");
        var sub = Dir("sub", @"\games\sub");
        sub.Children.Add(File("target.txt", @"\games\sub\target.txt"));
        root.Children.Add(sub);
        var list = new ObservableCollection<SftpEntry> { root };

        var found = FileExplorerViewModel.FindEntry(list, @"\games\sub\target.txt");

        Assert.NotNull(found);
        Assert.Equal("target.txt", found!.Name);
    }

    [Fact]
    public void FindEntry_CaseInsensitive()
    {
        var root = Dir("games", @"\games");
        root.Children.Add(File("a.txt", @"\games\a.txt"));
        var list = new ObservableCollection<SftpEntry> { root };

        Assert.NotNull(FileExplorerViewModel.FindEntry(list, @"\GAMES\A.TXT"));
    }

    [Fact]
    public void FindEntry_Missing_ReturnsNull()
    {
        var list = new ObservableCollection<SftpEntry> { Dir("games", @"\games") };

        Assert.Null(FileExplorerViewModel.FindEntry(list, @"\games\nope.txt"));
    }

    // ---- CollectExpandedPaths ----

    [Fact]
    public void CollectExpandedPaths_CollectsExpandedRecursively()
    {
        var root = Dir("games", @"\games");
        var sub = Dir("sub", @"\games\sub");
        root.Children.Add(sub);
        sub.Children.Add(File("a.txt", @"\games\sub\a.txt"));
        var list = new ObservableCollection<SftpEntry> { root };
        root.IsExpanded = true;
        sub.IsExpanded = true;

        var paths = FileExplorerViewModel.CollectExpandedPaths(list);

        Assert.Equal(new[] { @"\games", @"\games\sub" }, paths.ToArray());
    }

    [Fact]
    public void CollectExpandedPaths_CollapsedNodeStopsTraversal()
    {
        var root = Dir("games", @"\games");
        var sub = Dir("sub", @"\games\sub");
        root.Children.Add(sub);
        sub.IsExpanded = true;
        var list = new ObservableCollection<SftpEntry> { root };
        root.IsExpanded = false;

        var paths = FileExplorerViewModel.CollectExpandedPaths(list);

        Assert.Empty(paths);
    }

    // ---- ClearTreeCache ----

    [Fact]
    public void ClearTreeCache_ResetsHasLoadedOnAll()
    {
        var root = Dir("games", @"\games");
        root.HasLoaded = true;
        var sub = Dir("sub", @"\games\sub");
        sub.HasLoaded = true;
        root.Children.Add(sub);
        var list = new ObservableCollection<SftpEntry> { root };

        FileExplorerViewModel.ClearTreeCache(list);

        Assert.False(root.HasLoaded);
        Assert.False(sub.HasLoaded);
    }

    // ---- FindParent ----

    [Fact]
    public void FindParent_ReturnsContainingEntry()
    {
        var target = File("a.txt", @"\games\a.txt");
        var games = Dir("games", @"\games");
        games.Children.Add(target);
        var list = new ObservableCollection<SftpEntry> { games };

        var parent = FileExplorerViewModel.FindParent(list, target);

        Assert.Same(games, parent);
    }

    [Fact]
    public void FindParent_RootLevelTarget_ReturnsNull()
    {
        var root = Dir("games", @"\games");
        var list = new ObservableCollection<SftpEntry> { root };

        Assert.Null(FileExplorerViewModel.FindParent(list, root));
    }

    [Fact]
    public void FindParent_NestedTarget_FindsGrandparent()
    {
        var target = File("a.txt", @"\a\b\a.txt");
        var b = Dir("b", @"\a\b");
        b.Children.Add(target);
        var a = Dir("a", @"\a");
        a.Children.Add(b);
        var list = new ObservableCollection<SftpEntry> { a };

        var parent = FileExplorerViewModel.FindParent(list, target);

        Assert.Same(b, parent);
    }

    // ---- GetParentPath ----

    [Theory]
    [InlineData(@"C:\a\b\", @"C:\a\")]
    [InlineData(@"C:\a\b", @"C:\a\")]
    [InlineData(@"C:\a\b\file.txt", @"C:\a\b\")]
    public void GetParentPath_ReturnsParent(string path, string expected)
    {
        Assert.Equal(expected, FileExplorerViewModel.GetParentPath(path));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:")]
    [InlineData(@"\")]
    public void GetParentPath_Root_ReturnsNull(string path)
    {
        Assert.Null(FileExplorerViewModel.GetParentPath(path));
    }
}
