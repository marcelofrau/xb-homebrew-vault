using System.Collections.ObjectModel;
using System.Globalization;
using XBVault.Models;

#nullable enable

namespace XBVault.Helpers;

public static class FileSystemPathParser
{
    public static string FormatBps(double bps)
    {
        if (bps >= 1024 * 1024)
            return $" {(bps / (1024 * 1024)).ToString("F1", CultureInfo.InvariantCulture)} MB/s";
        if (bps >= 1024)
            return $" {(bps / 1024).ToString("F1", CultureInfo.InvariantCulture)} KB/s";
        return $" {bps.ToString("F0", CultureInfo.InvariantCulture)} B/s";
    }

    public static void InsertSorted(ObservableCollection<SftpEntry> list, SftpEntry entry)
    {
        var i = 0;
        for (; i < list.Count; i++)
        {
            var e = list[i];
            if (e.IsPlaceholder) continue;
            if (entry.IsDirectory && !e.IsDirectory) break;
            if (!entry.IsDirectory && e.IsDirectory) continue;
            if (string.Compare(entry.Name, e.Name, StringComparison.OrdinalIgnoreCase) < 0) break;
        }
        list.Insert(i, entry);
        UpdateLastChildFlag(list);
    }

    public static void UpdateLastChildFlag(ObservableCollection<SftpEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i].IsLastChild = i >= entries.Count - 1;
    }

    public static void UpdateChildrenPathsRecursive(SftpEntry entry, string oldPath, string newPath)
    {
        foreach (var child in entry.Children)
        {
            if (child.IsPlaceholder) continue;
            child.FullPath = child.FullPath.Replace(oldPath, newPath);
            if (child.IsDirectory)
                UpdateChildrenPathsRecursive(child, oldPath, newPath);
        }
    }

    public static List<string> CollectExpandedPaths(ObservableCollection<SftpEntry> entries)
    {
        var paths = new List<string>();
        foreach (var e in entries)
        {
            if (e.IsExpanded)
                paths.Add(e.FullPath);
            if (e.IsExpanded && e.Children.Count > 0)
                paths.AddRange(CollectExpandedPaths(e.Children));
        }
        return paths;
    }

    public static void ClearTreeCache(ObservableCollection<SftpEntry> entries)
    {
        foreach (var e in entries)
        {
            e.HasLoaded = false;
            if (e.Children.Count > 0)
                ClearTreeCache(e.Children);
        }
    }

    public static SftpEntry? FindEntry(ObservableCollection<SftpEntry> entries, string path)
    {
        foreach (var e in entries)
        {
            if (e.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                return e;
            if (e.Children.Count > 0)
            {
                var found = FindEntry(e.Children, path);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    public static string? GetParentPath(string path)
    {
        var trimmed = path.TrimEnd('\\');
        var idx = trimmed.LastIndexOf('\\');
        if (idx <= 0) return null;
        return trimmed[..idx] + "\\";
    }

    public static SftpEntry? FindParent(ObservableCollection<SftpEntry> entries, SftpEntry target)
    {
        foreach (var e in entries)
        {
            if (e.Children.Contains(target))
                return e;
            if (e.Children.Count > 0)
            {
                var found = FindParent(e.Children, target);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    public static string[] BuildBreadcrumbSegments(string currentPath)
    {
        var parts = currentPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return [];
        var segments = new string[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var joined = string.Join("\\", parts, 0, i + 1);
            if (joined.EndsWith(':'))
                joined += "\\";
            segments[i] = joined;
        }
        return segments;
    }
}
