using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class ProgressReadStreamTests
{
    private static MemoryStream MakeStream(byte[] data)
    {
        var ms = new MemoryStream(data);
        return ms;
    }

    private sealed class SyncProgress : IProgress<double>
    {
        private readonly Action<double> _handler;
        public SyncProgress(Action<double> handler) => _handler = handler;
        public void Report(double value) => _handler(value);
    }

    [Fact]
    public void ReportsIntermediateProgress_WhenReading()
    {
        var bytes = new byte[1000];
        new System.Random(42).NextBytes(bytes);
        using var inner = MakeStream(bytes);
        var reports = new System.Collections.Generic.List<double>();
        var progress = new SyncProgress(reports.Add);
        using var wrapped = new ProgressReadStream(inner, progress);

        var buffer = new byte[250];
        var total = 0;
        while (total < bytes.Length)
            total += wrapped.Read(buffer, 0, buffer.Length);

        Assert.NotEmpty(reports);
        Assert.True(reports[0] > 0 && reports[0] < 1.0, "progress must move before stream ends");
        Assert.Equal(bytes.Length, total);
        Assert.Equal(1.0, reports[^1]);
    }

    [Fact]
    public async Task ReportsProgress_OverAsyncRead()
    {
        var bytes = new byte[800];
        using var inner = MakeStream(bytes);
        var reports = new System.Collections.Generic.List<double>();
        var progress = new SyncProgress(reports.Add);
        using var wrapped = new ProgressReadStream(inner, progress);

        var buffer = new byte[200];
        var total = 0;
        int read;
        while ((read = await wrapped.ReadAsync(buffer.AsMemory(), CancellationToken.None)) > 0)
            total += read;

        Assert.Equal(800, total);
        Assert.True(reports.Count >= 4);
        Assert.True(reports[0] > 0 && reports[0] < 1.0, "progress must move before stream ends");
        Assert.Equal(1.0, reports[^1]);
    }

    [Fact]
    public void PreservesInnerData_AcrossFullRead()
    {
        var payload = Encoding.UTF8.GetBytes("xb-homebrew-vault fake 500MB upload body");
        using var inner = MakeStream(payload);
        using var wrapped = new ProgressReadStream(inner, null);

        using var outMs = new MemoryStream();
        wrapped.CopyTo(outMs);

        Assert.Equal(payload, outMs.ToArray());
    }

    [Fact]
    public void TotalDrivesFromInnerLength()
    {
        using var inner = MakeStream(new byte[512]);
        using var wrapped = new ProgressReadStream(inner, null);
        Assert.Equal(512, wrapped.Length);
    }
}
