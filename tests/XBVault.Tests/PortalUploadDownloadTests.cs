using System.Net;
using System.Reflection;
using System.Text;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class PortalUploadDownloadTests
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

    private sealed class SyncProg : IProgress<double>
    {
        private readonly Action<double> _onReport;
        public SyncProg(Action<double> onReport) => _onReport = onReport;
        public void Report(double value) => _onReport(value);
    }

    [Fact]
    public async Task UploadFileAsync_StreamsBodyToPortal_AndReportsByteProgress()
    {
        var payload = new byte[150_000];
        new Random(7).NextBytes(payload);
        var localFile = Path.Combine(Path.GetTempPath(), $"xbv_up_{Guid.NewGuid():N}.bin");
        var drainedBytes = -1L;
        string? requestedUrl = null;
        await File.WriteAllBytesAsync(localFile, payload);

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery.StartsWith("/api/os/info"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{}", Encoding.UTF8, "application/json") };

            if (request.RequestUri.PathAndQuery.StartsWith("/api/filesystem/apps/file"))
            {
                requestedUrl = request.RequestUri.PathAndQuery;
                // Simulate a real transport: drain the multipart body so
                // ProgressReadStream actually advances during the upload.
                using var src = request.Content!.ReadAsStreamAsync().GetAwaiter().GetResult();
                using var ms = new MemoryStream();
                src.CopyTo(ms);
                drainedBytes = ms.Length;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        try
        {
            var portal = new PortalAppFilesService(CreateAuth(handler), null!);
            var reports = new List<double>();
            var progress = new SyncProg(reports.Add);

            await portal.UploadFileAsync(@"UserFiles:\LocalAppData\mypkg", localFile, progress);

            Assert.NotNull(requestedUrl);
            Assert.StartsWith("/api/filesystem/apps/file?", requestedUrl);
            Assert.Contains("knownfolderid=LocalAppData", requestedUrl);
            Assert.Contains("packagefullname=mypkg", requestedUrl);
            Assert.Contains("extract=false", requestedUrl);
            Assert.True(drainedBytes >= payload.Length, $"multipart body must carry the full file (drained {drainedBytes} >= {payload.Length})");
            Assert.True(reports[^1] >= 1.0, "progress must reach 1.0 when upload completes");
            Assert.True(reports.Any(r => r > 0 && r < 1.0), "progress must move mid-upload, not just at the end");
        }
        finally
        {
            if (File.Exists(localFile)) File.Delete(localFile);
        }
    }

    [Fact]
    public async Task UploadFileAsync_NullKnownFolder_Throws()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery.StartsWith("/api/os/info"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var portal = new PortalAppFilesService(CreateAuth(handler), null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => portal.UploadFileAsync("UserFiles:\\", "C:\\whatever.bin"));
    }

    [Fact]
    public async Task DownloadFileToStreamAsync_StreamsContent_WithProgress()
    {
        var body = new byte[300_000];
        new Random(9).NextBytes(body);
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery.StartsWith("/api/filesystem/apps/file"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new ByteArrayContent(body) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var portal = new PortalAppFilesService(CreateAuth(handler), null!);
        await using var dst = new MemoryStream();
        var reports = new List<double>();
        var progress = new SyncProg(reports.Add);
        var file = new Models.SftpEntry
        {
            Name = "big.bin",
            FullPath = @"UserFiles:\LocalAppData\gen1recomp\LocalState\big.bin",
            IsDirectory = false,
            IsPortal = true
        };

        await portal.DownloadFileToStreamAsync(file, dst, progress);

        Assert.Equal(body, dst.ToArray());
        Assert.True(reports.Count > 1);
        Assert.Contains(reports, r => r > 0 && r < 1.0);
        Assert.Equal(1.0, reports[^1]);
    }

    [Fact]
    public async Task DownloadFileToStreamAsync_HttpError_Throws()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery.StartsWith("/api/filesystem/apps/file"))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                { Content = new StringContent("boom") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var portal = new PortalAppFilesService(CreateAuth(handler), null!);
        var file = new Models.SftpEntry
        {
            Name = "x.bin",
            FullPath = @"UserFiles:\LocalAppData\pkg\LocalState\x.bin",
            IsDirectory = false,
            IsPortal = true
        };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            portal.DownloadFileToStreamAsync(file, new MemoryStream(), null));
    }
}
