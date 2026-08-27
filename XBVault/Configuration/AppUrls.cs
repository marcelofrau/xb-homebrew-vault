using System.Globalization;
using System.Text;

namespace XBVault;

/// <summary>
/// Central registry of URLs referenced by the app — API endpoints, feeds and
/// external links. Change a host/template here instead of hunting strings.
/// </summary>
public static class AppUrls
{
    public const string CatalogJson =
        "https://emulationrevival.github.io/api/catalog.json";

    public const string EmulationRevival =
        "https://emulationrevival.github.io";

    public const string ReleaseApiLatest =
        "https://api.github.com/repos/marcelofrau/xb-homebrew-vault/releases/latest";

    public const string OverridesJson =
        "https://raw.githubusercontent.com/marcelofrau/xb-homebrew-vault/main/XBVault/Assets/package-overrides.json";

    public const string GitHubRepo =
        "https://github.com/marcelofrau/xb-homebrew-vault";

    public const string GitHubReleases =
        "https://github.com/marcelofrau/xb-homebrew-vault/releases";

    public const string LegacyDocsSite =
        "https://marcelofrau.github.io/xb-homebrew-vault/";

    public const string InspectorDocs =
        "https://xbvault.pages.dev/inspector";

    public const string GoFileServers =
        "https://api.gofile.io/servers";

    public const string GoFileUploadTemplate =
        "https://{0}.gofile.io/contents/uploadfile";

    public const string GoFileContentsTemplate =
        "https://{0}.gofile.io/contents/{1}";

    public const string DriveDownloadTemplate =
        "https://drive.google.com/uc?export=download&id={0}";

    public const string DiscordRevives =
        "https://discord.gg/cBYsQCS7j7";

    public const string DiscordXboxHub =
        "https://discord.gg/pVd47KAG24";

    public const string DiscordEmuRevival =
        "https://discord.gg/j2HndpJTej";

    public const string UwxXrayDepotRepo =
        "https://github.com/marcelofrau/uwp-xray-depot";

    public const string XrayPyConnector =
        "https://github.com/marcelofrau/xb-xray-py-connector";

    public const string XFilesUwp =
        "https://github.com/marcelofrau/x-files-uwp";

    public static string GoFileUpload(string server) => string.Format(CultureInfo.InvariantCulture, GoFileUploadFormat, server);

    public static string GoFileContents(string server, string contentId) => string.Format(CultureInfo.InvariantCulture, GoFileContentsFormat, server, contentId);

    public static string DriveDownload(string fileId) => string.Format(CultureInfo.InvariantCulture, DriveDownloadFormat, fileId);

    private static readonly CompositeFormat GoFileUploadFormat = CompositeFormat.Parse(GoFileUploadTemplate);
    private static readonly CompositeFormat GoFileContentsFormat = CompositeFormat.Parse(GoFileContentsTemplate);
    private static readonly CompositeFormat DriveDownloadFormat = CompositeFormat.Parse(DriveDownloadTemplate);
}
