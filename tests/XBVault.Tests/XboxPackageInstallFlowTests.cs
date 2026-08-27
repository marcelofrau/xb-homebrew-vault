using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using XBVault.Services;

namespace XBVault.Tests;

public class XboxPackageInstallFlowTests : IDisposable
{
    private readonly string _dir;

    private const string DevHomePfn = "Microsoft.Xbox.DevHome_1.0.2607.19001_x64__8wekyb3d8bbwe";
    private const string TargetPfn = "Gen1RecompUWP_1.0.0.0_x64__abc123";

    private const string ResourceInUseReason =
        "error 0x80073D02: Unable to install because the following apps need to be closed ";

    public XboxPackageInstallFlowTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string CreateAppx(string name, string identityName)
    {
        var path = Path.Combine(_dir, name);
        using (var fs = File.Create(path))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("AppxManifest.xml", CompressionLevel.NoCompression);
            using var es = entry.Open();
            var manifest = $"<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\"><Identity Name=\"{identityName}\" Version=\"0.2.29.0\" ProcessorArchitecture=\"x64\" Publisher=\"CN=Test\" /></Package>";
            es.Write(Encoding.UTF8.GetBytes(manifest), 0, manifest.Length);
        }
        return path;
    }

    private string CreateText(string name, string content = "test file")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static (XboxPackageService svc, StubHttpMessageHandler handler, Portal portal) CreateService(Portal portal)
    {
        var auth = new XboxAuthService();
        auth.Configure("http://xbox.dev:11443", "DevToolsUser", "test123");

        var handler = new StubHttpMessageHandler(portal.Respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://xbox.dev:11443") };
        var authField = typeof(XboxAuthService).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_http field not found");
        authField.SetValue(auth, http);

        var svc = new XboxPackageService(auth)
        {
            MainPollTimeout = TimeSpan.FromSeconds(1),
            DepPollTimeout = TimeSpan.FromSeconds(1),
            IdlePollTimeout = TimeSpan.FromSeconds(1),
            PollDelay = TimeSpan.FromMilliseconds(1),
            RetryDelay = TimeSpan.FromMilliseconds(1)
        };
        return (svc, handler, portal);
    }

    private sealed class Portal
    {
        public string BlockMode = "off"; // off | devhome | target | deploying
        public bool SkipBlockerOnceAfterDepUpload;
        public bool DepUploaded;
        public int UploadCount;
        private bool _blockerConsumed;

        public string PackagesJson { get; set; } =
            "{\"HolographicAvailable\":false,\"InstalledPackages\":[{\"PackageFullName\":\"Gen1RecompUWP_0.2.29.0_x64__hbddzpzx5cgwg\",\"Name\":\"Gen1Recomp\",\"PackageFamilyName\":\"Gen1RecompUWP\"}]}";

        public HttpResponseMessage Respond(HttpRequestMessage req)
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (path == "/api/app/packagemanager/state") return StateResponse();
            if (path == "/api/app/packagemanager/package")
            {
                UploadCount++;
                DepUploaded = UploadCount >= 2;
                return Json("{\"Success\":true}", HttpStatusCode.Accepted);
            }
            if (path == "/api/app/packagemanager/packages")
                return Json(PackagesJson);
            if (path == "/api/taskmanager/app" && req.Method == HttpMethod.Delete)
                return Json("{\"Success\":true}");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private HttpResponseMessage StateResponse()
        {
            if (SkipBlockerOnceAfterDepUpload && DepUploaded && !_blockerConsumed)
            {
                _blockerConsumed = true;
                return Json(BlockerBody, HttpStatusCode.OK);
            }
            return BlockMode switch
            {
                "devhome" => Json(BlockerBody, HttpStatusCode.OK),
                "target" => Json(BlockerTargetBody, HttpStatusCode.OK),
                "deploying" => Json("{\"Reason\":\"deployment in progress\"}", HttpStatusCode.OK),
                _ => new HttpResponseMessage(HttpStatusCode.NoContent)
            };
        }

        public string BlockerBody => $"{{\"Code\":-2147009278,\"CodeText\":\"The package could not be installed because resources it modifies are currently in use.\\r\\n\",\"Reason\":\"{ResourceInUseReason}{DevHomePfn}.\",\"Success\":false}}";

        public string BlockerTargetBody => $"{{\"Code\":-2147009278,\"CodeText\":\"The package could not be installed because resources it modifies are currently in use.\\r\\n\",\"Reason\":\"{ResourceInUseReason}{TargetPfn}.\",\"Success\":false}}";

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            var resp = new HttpResponseMessage(status);
            if (!string.IsNullOrEmpty(json))
                resp.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return resp;
        }
    }

    [Fact]
    public async Task Install_DependencyInUse_IsSkippedLikePresent_WithoutKill()
    {
        var portal = new Portal
        {
            BlockMode = "off",
            SkipBlockerOnceAfterDepUpload = true
        };
        var (svc, handler, _) = CreateService(portal);
        var main = CreateAppx("Gen1RecompUWP.appx", "Gen1RecompUWP");
        var dep = CreateText("Microsoft.VCLibs.x64.14.00.appx");

        var ok = await svc.InstallPackageAsync(main, [dep]);

        Assert.True(ok);
        var kills = handler.Requests
            .Where(r => r.Method == HttpMethod.Delete && r.RequestUri?.AbsolutePath == "/api/taskmanager/app")
            .ToList();
        Assert.Empty(kills);
    }

    [Fact]
    public async Task Install_MainBlockedByNonTarget_FailsWithoutKillingBlocker()
    {
        var portal = new Portal { BlockMode = "devhome", PackagesJson = "{\"InstalledPackages\":[]}" };
        var (svc, handler, _) = CreateService(portal);
        var main = CreateAppx("Gen1RecompUWP.appx", "Gen1RecompUWP");

        var ok = await svc.InstallPackageAsync(main, []);

        Assert.False(ok);
        var kills = handler.Requests
            .Where(r => r.Method == HttpMethod.Delete && r.RequestUri?.AbsolutePath == "/api/taskmanager/app")
            .ToList();
        Assert.Empty(kills);
    }

    [Fact]
    public async Task Install_MainBlockedByOwnTarget_KillsOnlyTarget_AndSucceeds()
    {
        var portal = new Portal { BlockMode = "target" };
        var (svc, handler, _) = CreateService(portal);
        var main = CreateAppx("Gen1RecompUWP.appx", "Gen1RecompUWP");

        var ok = await svc.InstallPackageAsync(main, []);

        Assert.True(ok);
        var targetB64 = Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(TargetPfn)));
        var kills = handler.Requests
            .Where(r => r.Method == HttpMethod.Delete && r.RequestUri?.AbsolutePath == "/api/taskmanager/app")
            .ToList();
        Assert.Contains(kills, r => (r.RequestUri?.Query ?? "").Contains(targetB64));
        Assert.DoesNotContain(kills, r => (r.RequestUri?.Query ?? "").Contains("DevHome"));
    }

    [Fact]
    public async Task Install_DependencyDeployTimeout_StillSucceedsWhenAppPresent()
    {
        var portal = new Portal { BlockMode = "deploying" };
        var (svc, handler, _) = CreateService(portal);
        var main = CreateAppx("Gen1RecompUWP.appx", "Gen1RecompUWP");
        var dep = CreateText("SomeDependency.appx");

        var ok = await svc.InstallPackageAsync(main, [dep]);

        Assert.True(ok);
    }

    [Fact]
    public async Task Install_UserCancelDuringWait_StillReportsTrueWhenInstalled()
    {
        var portal = new Portal { BlockMode = "devhome" };
        var (svc, handler, _) = CreateService(portal);
        var main = CreateAppx("Gen1RecompUWP.appx", "Gen1RecompUWP");

        using var cts = new CancellationTokenSource();
        _ = Task.Run(async () => { await Task.Delay(200); cts.Cancel(); });

        var ok = await svc.InstallPackageAsync(main, [], cancellationToken: cts.Token);

        Assert.True(ok);
        Assert.Contains(handler.Requests, r => r.RequestUri?.AbsolutePath == "/api/app/packagemanager/packages");
    }

    [Fact]
    public void FilterBlockingTargets_OnlyKeepsTheInstallTarget()
    {
        var all = new List<string> { DevHomePfn, TargetPfn, "Xbox.IdleScreen_2607.0.0.0_x64__8wekyb3d8bbwe" };
        var targets = XboxPackageService.FilterBlockingTargets("Gen1RecompUWP", all);

        Assert.Single(targets);
        Assert.Equal(TargetPfn, targets[0]);
    }
}
