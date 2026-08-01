using System.Runtime.CompilerServices;
using XBVault.Services;

namespace XBVault.Tests;

internal static class TestInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        Logger.MinLevel = LogLevel.Fatal;
    }
}
