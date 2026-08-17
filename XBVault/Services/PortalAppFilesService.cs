#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

/// <summary>
/// Browsing client for the Xbox Dev Portal filesystem endpoints
/// (<c>/api/filesystem/apps/*</c>). Exposes app LocalAppData / DevelopmentFiles
/// as a read-only virtual tree rooted at <c>UserFiles:\</c>.
/// </summary>
public class PortalAppFilesService : IDisposable
{
    public const string RootName = "User Files";
    public const string RootPath = "UserFiles:\\";
    public const string DevelopmentFiles = "DevelopmentFiles";
    public const string LocalAppData = "LocalAppData";

    private static readonly string[] FallbackKnownFolders = [DevelopmentFiles, LocalAppData];

    private readonly XboxAuthService _auth;
    private readonly IXboxPackageService _packageService;
    private CancellationTokenSource? _cts;

    public PortalAppFilesService(XboxAuthService auth, IXboxPackageService packageService)
    {
        _auth = auth;
        _packageService = packageService;
    }

    public static bool IsPortalPath(string path) =>
        path.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the path targets a specific app package (e.g. <c>UserFiles:\LocalAppData\&lt;pkg&gt;\...</c>),
    /// so portal folder creation is valid. Root and package-list levels have no package context.
    /// </summary>
    public static bool HasPackageContext(string path)
    {
        var (knownFolder, package, _) = Parse(path);
        return knownFolder?.Equals(LocalAppData, StringComparison.OrdinalIgnoreCase) == true && package is not null;
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _cts = null;
    }

    public void Dispose()
    {
        Cancel();
        GC.SuppressFinalize(this);
    }

    private CancellationToken BeginOperation()
    {
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    public async Task<List<SftpEntry>> ListDirectoryAsync(string path)
    {
        var token = BeginOperation();
        var (knownFolder, package, dirParts) = Parse(path);

        if (knownFolder is null)
            return await ListKnownFoldersAsync(token);

        if (knownFolder.Equals(LocalAppData, StringComparison.OrdinalIgnoreCase) && package is null)
            return await ListPackagesAsync(token);

        return await ListFromApiAsync(knownFolder, package, BuildPortalPath(dirParts), path, token);
    }

    public async Task<List<SftpEntry>> RecursiveListAsync(string path)
    {
        var token = BeginOperation();
        var (knownFolder, package, dirParts) = Parse(path);
        if (knownFolder is null)
            throw new InvalidOperationException("Cannot recursively list the portal root");

        var all = new List<SftpEntry>();
        var queue = new Queue<(string TreePath, string PortalPath)>();
        queue.Enqueue((path.TrimEnd('\\'), BuildPortalPath(dirParts)));

        while (queue.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var (treePath, portalPath) = queue.Dequeue();
            var items = await ListFromApiAsync(knownFolder, package, portalPath, treePath, token);
            foreach (var item in items)
            {
                all.Add(item);
                if (item.IsDirectory)
                {
                    var (_, _, itemDirParts) = Parse(item.FullPath);
                    queue.Enqueue((item.FullPath, BuildPortalPath(itemDirParts)));
                }
            }
        }

        return all;
    }

    public async Task DownloadFileAsync(SftpEntry file, string destinationPath, IProgress<double>? progress = null)
    {
        var token = BeginOperation();
        var (knownFolder, package, dirParts) = Parse(file.FullPath);
        if (knownFolder is null)
            throw new InvalidOperationException("Cannot download the portal root");

        if (dirParts.Count > 0)
            dirParts.RemoveAt(dirParts.Count - 1);
        var portalPath = BuildPortalPath(dirParts);
        var url = $"/api/filesystem/apps/file?knownfolderid={Uri.EscapeDataString(knownFolder)}" +
                  $"&filename={Uri.EscapeDataString(file.Name)}" +
                  $"&packagefullname={Uri.EscapeDataString(package ?? "")}" +
                  $"&path={Uri.EscapeDataString(portalPath)}";
        Logger.Debug($"PortalAppFiles.Download: {url}");

        using var response = await _auth.Http.GetAsync(url, token);
        if (!response.IsSuccessStatusCode)
        {
            var body = await _auth.ReadResponseBody(response);
            Logger.Warn($"PortalAppFiles.Download failed: {(int)response.StatusCode} — {body}");
            throw new HttpRequestException($"Portal download failed: HTTP {(int)response.StatusCode} — {body}");
        }

        try
        {
            await using var src = await response.Content.ReadAsStreamAsync(token);
            await using var dst = File.Create(destinationPath);
            var total = response.Content.Headers.ContentLength ?? file.Size;
            var buffer = new byte[81920];
            long copied = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, token)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), token);
                copied += read;
                if (total > 0)
                    progress?.Report((double)copied / total);
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
            }
            catch (Exception ex)
            {
                Logger.Trace($"PortalAppFiles: partial download cleanup failed — {ex.Message}");
            }
            throw;
        }
    }

    public async Task UploadFileAsync(string targetPath, string localFilePath)
    {
        var (knownFolder, package, dirParts) = Parse(targetPath);
        if (knownFolder is null)
            throw new InvalidOperationException("Cannot upload to the portal root");

        var fileName = Path.GetFileName(localFilePath);
        var url = $"/api/filesystem/apps/file?knownfolderid={Uri.EscapeDataString(knownFolder)}" +
                  $"&packagefullname={Uri.EscapeDataString(package ?? "")}" +
                  $"&path={Uri.EscapeDataString(BuildPortalPath(dirParts))}&extract=false";
        Logger.Debug($"PortalAppFiles.Upload: {url} ({fileName})");

        using var fileStream = File.OpenRead(localFilePath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentDisposition = ContentDispositionHeaderValue.Parse(
            "form-data; name=\"file\"; filename=\"" + fileName.Replace("\"", "\\\"") + "\"");
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent);

        using var response = await _auth.PostWithCsrfAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await _auth.ReadResponseBody(response);
            Logger.Warn($"PortalAppFiles.Upload failed: {(int)response.StatusCode} — {body}");
            throw new HttpRequestException($"Portal upload failed: HTTP {(int)response.StatusCode} — {body}");
        }
    }

    /// <summary>
    /// Uploads the contents of a local folder (recursively) into an existing portal
    /// directory. Sub-directories are created through the portal folder endpoint.
    /// </summary>
    public async Task UploadTreeAsync(string targetPath, string localFolder)
    {
        foreach (var subDir in Directory.EnumerateDirectories(localFolder))
        {
            var dirName = Path.GetFileName(subDir);
            await CreateFolderAsync(targetPath, dirName);
            await UploadTreeAsync(targetPath.TrimEnd('\\') + "\\" + dirName, subDir);
        }
        foreach (var file in Directory.EnumerateFiles(localFolder))
            await UploadFileAsync(targetPath, file);
    }

    public async Task CreateFolderAsync(string folderPath, string folderName)
    {
        var (knownFolder, package, dirParts) = Parse(folderPath);
        if (knownFolder is null)
            throw new InvalidOperationException("Cannot create a folder at the portal root");

        var url = $"/api/filesystem/apps/folder?knownfolderid={Uri.EscapeDataString(knownFolder)}" +
                  $"&newfoldername={Uri.EscapeDataString(folderName)}" +
                  $"&packagefullname={Uri.EscapeDataString(package ?? "")}" +
                  $"&path={Uri.EscapeDataString(BuildPortalPath(dirParts))}";
        Logger.Debug($"PortalAppFiles.CreateFolder: {url}");

        using var response = await _auth.PostWithCsrfAsync(url, null);
        if (!response.IsSuccessStatusCode)
        {
            var body = await _auth.ReadResponseBody(response);
            Logger.Warn($"PortalAppFiles.CreateFolder failed: {(int)response.StatusCode} — {body}");
            throw new HttpRequestException($"Portal folder create failed: HTTP {(int)response.StatusCode} — {body}");
        }
    }

    public async Task RenameEntryAsync(string entryPath, string newName)
    {
        var (knownFolder, package, dirParts) = Parse(entryPath);
        if (knownFolder is null)
            throw new InvalidOperationException("Cannot rename the portal root");

        var oldName = dirParts.Count > 0 ? dirParts[^1] : entryPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrEmpty(oldName))
            throw new InvalidOperationException("Cannot rename the portal root");

        var url = $"/api/filesystem/apps/rename?knownfolderid={Uri.EscapeDataString(knownFolder)}" +
                  $"&filename={Uri.EscapeDataString(oldName)}" +
                  $"&newfilename={Uri.EscapeDataString(newName)}" +
                  $"&packagefullname={Uri.EscapeDataString(package ?? "")}" +
                  $"&path={Uri.EscapeDataString(BuildPortalPath(dirParts))}";
        Logger.Debug($"PortalAppFiles.Rename: {url}");

        using var response = await _auth.PostWithCsrfAsync(url, null);
        if (!response.IsSuccessStatusCode)
        {
            var body = await _auth.ReadResponseBody(response);
            Logger.Warn($"PortalAppFiles.Rename failed: {(int)response.StatusCode} — {body}");
            throw new HttpRequestException($"Portal rename failed: HTTP {(int)response.StatusCode} — {body}");
        }
    }

    public async Task DeleteEntryAsync(string entryPath)
    {
        var (knownFolder, package, dirParts) = Parse(entryPath);
        if (knownFolder is null)
            throw new InvalidOperationException("Cannot delete the portal root");

        var fileName = dirParts.Count > 0 ? dirParts[^1] : entryPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrEmpty(fileName))
            throw new InvalidOperationException("Cannot delete the portal root");

        var parentParts = dirParts.Count > 0 ? dirParts.Take(dirParts.Count - 1).ToList() : [];
        var url = $"/api/filesystem/apps/file?knownfolderid={Uri.EscapeDataString(knownFolder)}" +
                  $"&filename={Uri.EscapeDataString(fileName)}" +
                  $"&packagefullname={Uri.EscapeDataString(package ?? "")}" +
                  $"&path={Uri.EscapeDataString(BuildPortalPath(parentParts))}";
        Logger.Debug($"PortalAppFiles.Delete: {url}");

        using var response = await _auth.DeleteWithCsrfAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var body = await _auth.ReadResponseBody(response);
            Logger.Warn($"PortalAppFiles.Delete failed: {(int)response.StatusCode} — {body}");
            throw new HttpRequestException($"Portal delete failed: HTTP {(int)response.StatusCode} — {body}");
        }
    }

    private async Task<List<SftpEntry>> ListKnownFoldersAsync(CancellationToken token)
    {
        var ids = await GetKnownFolderIdsAsync(token);
        var entries = ids.Select(id => new SftpEntry
        {
            Name = id,
            FullPath = RootPath + id,
            IsDirectory = true,
            IsPortal = true,
            Children = { new SftpEntry { Name = "" } }
        }).ToList();
        SetIsLastChild(entries);
        return entries;
    }

    private async Task<List<SftpEntry>> ListPackagesAsync(CancellationToken token)
    {
        var packages = (await _packageService.GetInstalledPackagesAsync())
            .Where(p => !string.IsNullOrEmpty(p.FullName) && p.Origin != 2)
            .OrderBy(p => p.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = packages.Select(p => new SftpEntry
        {
            Name = p.FullName,
            FullPath = RootPath + LocalAppData + "\\" + p.FullName,
            IsDirectory = true,
            IsPortal = true,
            ToolTip = p.DisplayName ?? p.Name,
            Children = { new SftpEntry { Name = "" } }
        }).ToList();
        SetIsLastChild(entries);
        return entries;
    }

    private async Task<List<SftpEntry>> ListFromApiAsync(string knownFolder, string? package, string portalPath, string treePath, CancellationToken token)
    {
        var url = $"/api/filesystem/apps/files?knownfolderid={Uri.EscapeDataString(knownFolder)}" +
                  $"&packagefullname={Uri.EscapeDataString(package ?? "")}" +
                  $"&path={Uri.EscapeDataString(portalPath)}";
        Logger.Debug($"PortalAppFiles.List: {url}");

        using var response = await _auth.Http.GetAsync(url, token);
        if (!response.IsSuccessStatusCode)
        {
            var body = await _auth.ReadResponseBody(response);
            Logger.Warn($"PortalAppFiles.List failed: {(int)response.StatusCode} — {body}");
            throw new HttpRequestException($"Portal file list failed: HTTP {(int)response.StatusCode} — {body}");
        }

            var json = await response.Content.ReadAsStringAsync(token);
        var parsed = JsonSerializer.Deserialize<FilesResponse>(json);
        var parent = treePath.TrimEnd('\\');
        var entries = (parsed?.Items ?? [])
            .Select(item => ItemToEntry(item, parent))
            .ToList();
        SetIsLastChild(entries);
        return entries;
    }

    private async Task<List<string>> GetKnownFolderIdsAsync(CancellationToken token)
    {
        try
        {
            using var response = await _auth.Http.GetAsync("/api/filesystem/apps/knownfolders", token);
            if (!response.IsSuccessStatusCode)
                return FallbackKnownFolders.ToList();

        var json = await response.Content.ReadAsStringAsync(token);
            var parsed = JsonSerializer.Deserialize<KnownFoldersResponse>(json);
            var ids = parsed?.KnownFolders ?? [];
            return ids.Count > 0 ? ids : FallbackKnownFolders.ToList();
        }
        catch (Exception ex)
        {
            Logger.Warn($"PortalAppFiles: knownfolders query failed — {ex.Message}");
            return FallbackKnownFolders.ToList();
        }
    }

    private static SftpEntry ItemToEntry(PortalFileItem item, string parentPath)
    {
        var isDir = (item.Type & 0x10) != 0;
        var entry = new SftpEntry
        {
            Name = item.Name,
            FullPath = parentPath + "\\" + item.Name,
            IsDirectory = isDir,
            IsPortal = true,
            Size = item.FileSize,
            Extension = isDir ? null : Path.GetExtension(item.Name)
        };
        if (item.DateCreated > 0)
        {
            try { entry.LastModified = DateTime.FromFileTimeUtc(item.DateCreated); }
            catch (ArgumentOutOfRangeException) { entry.LastModified = DateTime.MinValue; }
        }
        if (isDir)
            entry.Children.Add(new SftpEntry { Name = "" });
        return entry;
    }

    /// <summary>
    /// Portal path format: root = "\", one level = "\\Settings", two levels = "\\Settings\\Sub".
    /// Each tree segment after the known folder (and package) is prefixed with a backslash.
    /// </summary>
    private static string BuildPortalPath(List<string> dirParts)
    {
        if (dirParts.Count == 0)
            return "\\";
        return "\\" + string.Concat(dirParts.Select(p => "\\" + p));
    }

    private static (string? KnownFolder, string? Package, List<string> DirParts) Parse(string path)
    {
        if (!path.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
            return (null, null, []);

        var rest = path.Substring(RootPath.Length).TrimEnd('\\');
        var parts = rest.Split('\\', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count == 0)
            return (null, null, []);

        var knownFolder = parts[0];
        var isAppData = knownFolder.Equals(LocalAppData, StringComparison.OrdinalIgnoreCase);
        var package = isAppData && parts.Count >= 2 ? parts[1] : null;
        var start = isAppData ? 2 : 1;
        return (knownFolder, package, parts.Skip(start).ToList());
    }

    private static void SetIsLastChild(List<SftpEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i].IsLastChild = i >= entries.Count - 1;
    }

    private sealed class FilesResponse
    {
        [JsonPropertyName("Items")]
        public List<PortalFileItem> Items { get; set; } = [];
    }

    private sealed class PortalFileItem
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Type")]
        public int Type { get; set; }

        [JsonPropertyName("FileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("DateCreated")]
        public long DateCreated { get; set; }
    }

    private sealed class KnownFoldersResponse
    {
        [JsonPropertyName("KnownFolders")]
        public List<string> KnownFolders { get; set; } = [];
    }
}
