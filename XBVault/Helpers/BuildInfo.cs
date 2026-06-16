using System.Reflection;

namespace XBVault.Helpers;

public static class BuildInfo
{
    public static string Version { get; } =
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

    public static string DisplayVersion => $"v{Version}";
}
