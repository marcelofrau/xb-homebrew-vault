using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using XBVault.Models;
using XBVault.Services;
using Xunit;

namespace XBVault.Tests;

public class FakeSftpProbeQuirksTests
{
    [Fact]
    public async Task Probe_Ignores_Stderr_Noise()
    {
        var fake = new FakeSftpService();
        var output = "Some warning message\nC:\nRandom error text\nD:\n";
        fake.OverrideRunShellResult(new SftpShellResult { Success = true, Output = output });

        var letters = await ParseLettersFromFake(fake);

        Assert.Equal(new[] { "C", "D" }, letters.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Probe_Handles_NonAscii_Labels()
    {
        var fake = new FakeSftpService();
        // include greek letter that should be ignored by our Latin-only filter
        var output = "C:\nD: Δ\nΕ:\n"; // Ε is Greek capital E
        fake.OverrideRunShellResult(new SftpShellResult { Success = true, Output = output });

        var letters = await ParseLettersFromFake(fake);

        Assert.Contains("C", letters);
        Assert.Contains("D", letters);
        Assert.DoesNotContain("Ε", letters); // Greek E should be excluded
    }

    [Fact]
    public async Task Probe_Ignores_Duplicates_And_Case()
    {
        var fake = new FakeSftpService();
        var output = "c:\nC:\n d:\nD:\n";
        fake.OverrideRunShellResult(new SftpShellResult { Success = true, Output = output });

        var letters = await ParseLettersFromFake(fake);

        Assert.Equal(2, letters.Count);
        Assert.Contains("C", letters);
        Assert.Contains("D", letters);
    }

    [Fact]
    public async Task Probe_Handles_Trailing_Backslashes()
    {
        var fake = new FakeSftpService();
        var output = "C:\\\nD:\\\n"; // C:\ and D:\
        fake.OverrideRunShellResult(new SftpShellResult { Success = true, Output = output });

        var letters = await ParseLettersFromFake(fake);

        Assert.Equal(new[] { "C", "D" }, letters.OrderBy(x => x).ToArray());
    }

    private static async Task<List<string>> ParseLettersFromFake(FakeSftpService fake)
    {
        var probe = "for %d in (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) do @vol %d: >nul 2>nul && echo %d:";
        var result = await fake.RunShellCommandAsync(probe);
        var rx = new System.Text.RegularExpressions.Regex("([A-Za-z])\\s*[:\\\\]");
        var letters = result.Output.Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim('\r', ' ', '\t'))
            .Select(l =>
            {
                var m = rx.Match(l);
                return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
            })
            .Where(s => s is not null)
            .Select(s => s!)
            .Distinct()
            .ToList();
        return letters;
    }
}
