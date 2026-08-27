using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using XBVault.Models;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class PortalDownloadZipTests
{
    private static XboxAuthService CreateAuth(StubHttpMessageHandler handler)
    {
        var auth = new XboxAuthService();
        auth.Configure("http://xbox.local:11443", "DevToolsUser", "pw");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://xbox.local:11443") };
        var flag = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(XboxAuthService).GetField("_http", flag)!.SetValue(auth, http);
        typeof(XboxAuthService).GetField("_transferHttp", flag)!.SetValue(auth, http);
        return auth;
    }

    private static StubHttpMessageHandler HandlerWith(
        string dataDocs,
        string backupDoc,
        string fileContent1,
        string fileContent2)
    {
        return new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.PathAndQuery ?? "";
            if (url.StartsWith("/api/filesystem/apps/files"))
            {
                var path = Uri.UnescapeDataString(request.RequestUri!.Query)
                    .Split("&").FirstOrDefault(p => p.StartsWith("path="))?.Substring(5) ?? "";
                var json = path.Contains("backup", StringComparison.OrdinalIgnoreCase)
                    ? backupDoc
                    : dataDocs;
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }
            if (url.StartsWith("/api/filesystem/apps/file"))
            {
                var filename = Uri.UnescapeDataString(request.RequestUri!.Query)
                    .Split("&").FirstOrDefault(p => p.StartsWith("filename="))?.Substring(9) ?? "";
                var body = filename.StartsWith("old", StringComparison.OrdinalIgnoreCase)
                    ? fileContent2
                    : fileContent1;
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }

    private static PortalAppFilesService NewPortal(StubHttpMessageHandler handler) =>
        new(CreateAuth(handler), null!);

    [Fact]
    public async Task FolderZippedRecursively_WithRelativeEntries()
    {
        var handler = HandlerWith(
            dataDocs: """{"Items":[{"Name":"sav.bin","Type":0,"FileSize":8,"DateCreated":0},{"Name":"backup","Type":16,"FileSize":0,"DateCreated":0}]}""",
            backupDoc: """{"Items":[{"Name":"old.bin","Type":0,"FileSize":9,"DateCreated":0}]}""",
            fileContent1: "sav-data",
            fileContent2: "old-data");

        var portal = NewPortal(handler);
        var tmp = Path.Combine(Path.GetTempPath(), $"xbv_ziptest_{Guid.NewGuid():N}.zip");
        try
        {
            var folder = new SftpEntry
            {
                Name = "data",
                FullPath = @"UserFiles:\LocalAppData\gen1recomp\data",
                IsDirectory = true,
                IsPortal = true
            };
            var progress = new List<double>();

            await portal.DownloadFolderAsZipAsync(folder, tmp, new SyncProg(progress.Add));

            Assert.True(File.Exists(tmp), "zip file must be created");
            using var zip = ZipFile.OpenRead(tmp);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Equal(2, names.Count);
            Assert.Contains("sav.bin", names);
            Assert.Contains("backup/old.bin", names);
            Assert.All(names, n => Assert.DoesNotContain('\\', n));

            using var sav = zip.GetEntry("sav.bin")!.Open();
            using var sr = new StreamReader(sav);
            Assert.Equal("sav-data", await sr.ReadToEndAsync());
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task EmptyFolder_ProducesEmptyZip()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.PathAndQuery ?? "";
            if (url.StartsWith("/api/filesystem/apps/files"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("""{"Items":[]}""", Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var portal = NewPortal(handler);
        var tmp = Path.Combine(Path.GetTempPath(), $"xbv_ziptest_{Guid.NewGuid():N}.zip");
        try
        {
            var folder = new SftpEntry
            {
                Name = "empty",
                FullPath = @"UserFiles:\LocalAppData\gen1recomp\empty",
                IsDirectory = true,
                IsPortal = true
            };

            await portal.DownloadFolderAsZipAsync(folder, tmp, null);

            using var zip = ZipFile.OpenRead(tmp);
            Assert.Empty(zip.Entries);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    private sealed class SyncProg : IProgress<double>
    {
        private readonly Action<double> _onReport;
        public SyncProg(Action<double> onReport) => _onReport = onReport;
        public void Report(double value) => _onReport(value);
    }
}
