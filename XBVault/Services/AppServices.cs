#nullable enable
using XBVault.ViewModels;

namespace XBVault.Services;

/// <summary>
/// Composition root: owns every service and view model for a single app run.
/// Both platform entry points (desktop + Android) build one instance through
/// <see cref="Create"/> and share it, so the ~20-line construction block is
/// not duplicated per platform.
/// </summary>
public sealed class AppServices
{
    public XboxAuthService Auth { get; }

    public XboxPackageService Package { get; }

    public XboxSystemService System { get; }

    public XboxNetworkService Network { get; }

    public XboxProcessService Process { get; }

    public XboxPerformanceService Performance { get; }

    public CacheService Cache { get; }

    public LocalOverrideService LocalOverride { get; }

    public PackageInstallService Install { get; }

    public SftpService Sftp { get; }

    public SftpTransferService SftpTransfer { get; }

    public PortalAppFilesService Portal { get; }

    public CatalogApiService Catalog { get; }

    public PackageOverrideService Override { get; }

    public VersionCheckerService VersionChecker { get; }

    public BackgroundTaskService BackgroundTasks { get; }

    public NotificationCenterService Notifications { get; }

    public InstalledAppUpdateService Update { get; }

    public MainViewModel Main { get; }

    public BrowseViewModel Browse { get; }

    public InstalledViewModel Installed { get; }

    public FileExplorerViewModel FileExplorer { get; }

    public ToolsViewModel Tools { get; }

    public SettingsViewModel Settings { get; }

    public TaskCenterViewModel TaskCenter { get; }

    private AppServices(
        XboxAuthService auth,
        XboxPackageService package,
        XboxSystemService system,
        XboxNetworkService network,
        XboxProcessService process,
        XboxPerformanceService performance,
        CacheService cache,
        LocalOverrideService localOverride,
        PackageInstallService install,
        SftpService sftp,
        SftpTransferService sftpTransfer,
        PortalAppFilesService portal,
        CatalogApiService catalog,
        PackageOverrideService packageOverride,
        VersionCheckerService versionChecker,
        BackgroundTaskService backgroundTaskService,
        NotificationCenterService notificationCenter,
        InstalledAppUpdateService update,
        MainViewModel main,
        BrowseViewModel browse,
        InstalledViewModel installed,
        FileExplorerViewModel fileExplorer,
        ToolsViewModel tools,
        SettingsViewModel settings,
        TaskCenterViewModel taskCenter)
    {
        Auth = auth;
        Package = package;
        System = system;
        Network = network;
        Process = process;
        Performance = performance;
        Cache = cache;
        LocalOverride = localOverride;
        Install = install;
        Sftp = sftp;
        SftpTransfer = sftpTransfer;
        Portal = portal;
        Catalog = catalog;
        Override = packageOverride;
        VersionChecker = versionChecker;
        BackgroundTasks = backgroundTaskService;
        Notifications = notificationCenter;
        Update = update;
        Main = main;
        Browse = browse;
        Installed = installed;
        FileExplorer = fileExplorer;
        Tools = tools;
        Settings = settings;
        TaskCenter = taskCenter;
    }

    public static AppServices Create()
    {
        var authService = new XboxAuthService();
        var packageService = new XboxPackageService(authService);
        var systemService = new XboxSystemService(authService);
        var networkService = new XboxNetworkService(authService);
        var processService = new XboxProcessService(authService);
        var performanceService = new XboxPerformanceService(authService);
        var cacheService = new CacheService();
        var localOverride = new LocalOverrideService();
        var installService = new PackageInstallService(cacheService, packageService, http: null, log: null, localOverride);
        var sftpService = new SftpService();
        var sftpTransferService = new SftpTransferService(sftpService);
        var portalService = new PortalAppFilesService(authService, packageService);
        var catalogService = new CatalogApiService();
        var overrideService = new PackageOverrideService();
        var versionChecker = new VersionCheckerService(overrideService, cache: null, localOverrideService: localOverride);
        var backgroundTaskService = new BackgroundTaskService();
        var notificationCenter = new NotificationCenterService();
        var taskCenterViewModel = new TaskCenterViewModel(backgroundTaskService);

        var mainViewModel = new MainViewModel(authService);
        var browseViewModel = new BrowseViewModel(installService, authService, packageService, catalogService, overrideService, versionChecker);
        var installedViewModel = new InstalledViewModel(authService, packageService);
        var fileExplorerViewModel = new FileExplorerViewModel(authService, sftpService, sftpTransferService, portalService);
        var toolsViewModel = new ToolsViewModel(authService, systemService);
        var settingsViewModel = new SettingsViewModel(authService, cacheService);

        var updateService = new InstalledAppUpdateService(authService, packageService, versionChecker, notificationCenter, backgroundTaskService);

        return new AppServices(
            authService, packageService, systemService, networkService, processService, performanceService,
            cacheService, localOverride, installService, sftpService, sftpTransferService, portalService,
            catalogService, overrideService, versionChecker, backgroundTaskService, notificationCenter,
            updateService, mainViewModel, browseViewModel, installedViewModel, fileExplorerViewModel,
            toolsViewModel, settingsViewModel, taskCenterViewModel);
    }

    /// <summary>Runs one-time boot side-effects that used to live in the App entry points.</summary>
    public void Initialize()
    {
        LocalOverride.Load();
        Override.Initialize();
        BackgroundTasks.Start();
    }
}
