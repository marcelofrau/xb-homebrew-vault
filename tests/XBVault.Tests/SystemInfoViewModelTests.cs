#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;
using Xunit;

namespace XBVault.Tests;

public class SystemInfoViewModelTests
{
    private sealed class FakeAuth : IXboxAuthService
    {
        public bool IsConnected { get; set; } = true;
        public event Action<bool>? ConnectionChanged;
        public bool IsConfigured => true;
        public string? SmbPassword => null;
        public string? Host => "192.168.0.1";
        public void Configure(string baseUrl, string username, string password) { }
        public SshConnectionInfo GetSshCredentials() => new("localhost", 22, "user", "pass");
        public Task<string?> FetchSmbPasswordAsync() => Task.FromResult<string?>(null);
        public string? GetDevPortalUrl() => null;
        public void MarkConnected() { }
        public void Disconnect() { }
        public Task<bool> EnsureConnectedAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
            => Task.FromResult(new ConnectionTestResult(true, 200, null));
        public void Dispose() { }
    }

    private sealed class FakeSystem : IXboxSystemService
    {
        public ConsoleInfo? Console { get; set; }
        public string? MachineName { get; set; }
        public List<XboxSetting> Settings { get; set; } = [];

        public Task<byte[]?> CaptureScreenshotAsync(CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        public Task<string?> GetSystemInfoAsync() => Task.FromResult<string?>(null);
        public Task<ConsoleInfo?> GetConsoleInfoAsync() => Task.FromResult(Console);
        public Task<string?> GetMachineNameAsync() => Task.FromResult(MachineName);
        public Task<IReadOnlyList<XboxSetting>> GetXboxSettingsAsync() => Task.FromResult<IReadOnlyList<XboxSetting>>(Settings);
        public Task<string?> GetCrashDumpsAsync() => Task.FromResult<string?>(null);
        public Task<bool> DeleteCrashDumpAsync(string filename) => Task.FromResult(true);
        public Task<string?> GetCrashControlAsync() => Task.FromResult<string?>(null);
        public Task<bool> SetCrashControlAsync(bool enabled) => Task.FromResult(true);
        public Task<bool> RestartXboxAsync() => Task.FromResult(true);
        public Task<bool> ShutdownXboxAsync() => Task.FromResult(true);
    }

    private static (SystemInfoViewModel vm, FakeAuth auth, FakeSystem sys) Build(
        ConsoleInfo? console = null,
        string? machine = null,
        List<XboxSetting>? settings = null)
    {
        var auth = new FakeAuth();
        var sys = new FakeSystem { Console = console, MachineName = machine, Settings = settings ?? [] };
        return (new SystemInfoViewModel(auth, sys), auth, sys);
    }

    private static ConsoleInfo FullConsole() => new()
    {
        ConsoleType = "Scorpio Dev Kit",
        OsVersion = "10.0.25398.3846",
        OsEdition = "WindowsDevKitEdition",
        DevMode = "Dev mode",
        ConsoleId = "CX-0000-00000-0000",
        DeviceId = "DEADBEEF-1234-5678-9ABC-DEF012345678",
        SerialNumber = "SN1234567",
        DevkitCertificateExpirationTime = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
    };

    private static SystemInfoRow Row(SystemInfoViewModel vm, string card, string label) =>
        vm.Cards.Single(c => c.Title == card).Rows.Single(r => r.Label == label);

    [Fact]
    public async Task Refresh_PopulatesIdentityPropsAndCards()
    {
        var (vm, _, _) = Build(FullConsole(), "XBRL",
        [
            new XboxSetting { Name = "TvResolution", Value = "3840x2160" },
            new XboxSetting { Name = "AllowHDR", Value = "true" },
            new XboxSetting { Name = "PowerMode", Value = "InstantOn" }
        ]);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Equal("XBRL", vm.MachineName);
        Assert.Equal("Scorpio Dev Kit", vm.ConsoleType);
        Assert.Equal("WindowsDevKitEdition", vm.OsEdition);
        Assert.Equal("Dev mode", vm.DevMode);
        Assert.Equal("CX-0000-00000-0000", vm.ConsoleId);
        Assert.Equal("SN1234567", vm.SerialNumber);
        Assert.Equal("DEADBEEF-1234-5678-9ABC-DEF012345678", vm.DeviceId);
        Assert.Equal("10.0.25398.3846", vm.OsVersion);
    }

    [Fact]
    public async Task Refresh_BuildsDisplayToggleRows()
    {
        var (vm, _, _) = Build(FullConsole(), null,
        [
            new XboxSetting { Name = "TvResolution", Value = "3840x2160" },
            new XboxSetting { Name = "AllowHDR", Value = "true" },
            new XboxSetting { Name = "AllowVRR", Value = "false" }
        ]);

        await vm.RefreshCommand.ExecuteAsync(null);

        var res = Row(vm, "Display", "TV Resolution");
        Assert.Equal("3840x2160", res.Value);
        Assert.True(res.IsHighlight);

        var hdr = Row(vm, "Display", "HDR");
        Assert.Equal("On", hdr.Value);
        Assert.True(hdr.IsPositive);

        var vrr = Row(vm, "Display", "VRR");
        Assert.Equal("Off", vrr.Value);
        Assert.True(vrr.IsNegative);
    }

    [Fact]
    public async Task Refresh_BuildsAllSixCards()
    {
        var (vm, _, _) = Build(FullConsole(), "XBRL");

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["Display", "Audio", "Power", "Network", "System & User", "Dev Kit"],
            vm.Cards.Select(c => c.Title));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(-5, true)]
    public async Task Refresh_FormatsDevkitCertText(int daysOffset, bool expired)
    {
        var exp = DateTimeOffset.UtcNow.AddDays(daysOffset).ToUnixTimeSeconds();
        var console = FullConsole();
        console.DevkitCertificateExpirationTime = exp;
        var (vm, _, _) = Build(console, null);

        await vm.RefreshCommand.ExecuteAsync(null);

        if (expired)
            Assert.StartsWith("Dev Mode expired", vm.DevkitCertText);
        else
            Assert.StartsWith("Dev Mode until", vm.DevkitCertText);
    }

    [Fact]
    public async Task Refresh_AllNull_ShowsError()
    {
        var (vm, _, _) = Build(null, null, []);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.NotNull(vm.StatusMessage);
    }

    [Fact]
    public async Task Refresh_MachineNameFallsBackToHostnameSetting()
    {
        var (vm, _, _) = Build(null, null,
        [
            new XboxSetting { Name = "Hostname", Value = "XBOX-SERIAL" }
        ]);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("XBOX-SERIAL", vm.MachineName);
        Assert.False(vm.HasError);
    }
}
