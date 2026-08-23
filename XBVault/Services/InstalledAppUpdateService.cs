#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public class InstalledAppUpdateService
{
    public const string JobName = "Check app updates";
    public const string UpdateNotificationTag = "app-updates";

    private readonly IXboxAuthService _authService;
    private readonly IXboxPackageService _packageService;
    private readonly VersionCheckerService _versionChecker;
    private readonly NotificationCenterService _notifications;
    private readonly BackgroundTaskService _backgroundTasks;
    private int _scanInFlight;

    public Func<CatalogItem, Task>? OpenUpdateDialogAsync { get; set; }

    public InstalledAppUpdateService(
        IXboxAuthService authService,
        IXboxPackageService packageService,
        VersionCheckerService versionChecker,
        NotificationCenterService notifications,
        BackgroundTaskService backgroundTasks)
    {
        _authService = authService;
        _packageService = packageService;
        _versionChecker = versionChecker;
        _notifications = notifications;
        _backgroundTasks = backgroundTasks;
    }

    public void Start()
    {
        _authService.ConnectionChanged += OnConnectionChanged;
        _backgroundTasks.RegisterJob(JobName, ReadInterval, RunScanAsync, canCancel: true);
    }

    public void Stop()
    {
        _authService.ConnectionChanged -= OnConnectionChanged;
    }

    private static TimeSpan ReadInterval()
    {
        try
        {
            return TimeSpan.FromMinutes(SettingsService.Current.UpdateCheckIntervalMinutes);
        }
        catch (Exception ex)
        {
            Logger.Debug($"InstalledAppUpdateService: interval read failed: {ex.Message}");
            return TimeSpan.FromMinutes(30);
        }
    }

    private void OnConnectionChanged(bool connected)
    {
        if (connected)
            _ = ScanAsync();
    }

    private async Task RunScanAsync(BackgroundTask task, CancellationToken ct)
    {
        await ScanAsync(ct);
    }

    public async Task ScanAsync(CancellationToken ct = default)
    {
        if (!_authService.IsConnected || !_versionChecker.HasCatalog)
            return;

        if (Interlocked.Exchange(ref _scanInFlight, 1) == 1)
            return;

        try
        {
            var packages = await _packageService.GetInstalledPackagesAsync();
            var outdated = new List<OutdatedPackage>();
            foreach (var pkg in packages)
            {
                ct.ThrowIfCancellationRequested();
                var op = _versionChecker.FindOutdated(pkg, ignoreSuppression: true);
                if (op is not null)
                    outdated.Add(op);
            }

            Logger.Trace($"InstalledAppUpdateService: scan found {outdated.Count} outdated of {packages.Count} packages");
            foreach (var op in outdated)
                Logger.Trace($"  {op.Catalog.Name}: {op.InstalledVersion} → {op.AvailableVersion}");

            if (outdated.Count == 0)
            {
                _notifications.DismissByTag(UpdateNotificationTag);
                return;
            }

            Notify(outdated);
        }
        catch (OperationCanceledException)
        {
            // scan cancelled — nothing to do
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "InstalledAppUpdateService: scan failed");
        }
        finally
        {
            Interlocked.Exchange(ref _scanInFlight, 0);
        }
    }

    private void Notify(List<OutdatedPackage> outdated)
    {
        var actions = outdated.Select(op => new NotificationAction
        {
            Label = $"{op.Catalog.Name} {op.InstalledVersion} → {op.AvailableVersion}",
            Action = () => OpenUpdateDialogAsync?.Invoke(op.Catalog)
        }).ToList();

        var title = outdated.Count == 1
            ? "1 app update available"
            : $"{outdated.Count} app updates available";

        var message = outdated.Count == 1
            ? "An installed app has a newer version."
            : $"{outdated.Count} installed apps have newer versions.";

        _notifications.NotifyGroupedReplacing(UpdateNotificationTag, title, actions,
            autoDismiss: false,
            iconUri: "avares://XBVault/Assets/Views/InstalledView/installed-update-20.png",
            message: message);
        Logger.Info($"InstalledAppUpdateService: notified {outdated.Count} app update(s): " +
            string.Join(", ", outdated.Select(op => $"{op.Catalog.Name}: {op.InstalledVersion} → {op.AvailableVersion}")));
    }
}
