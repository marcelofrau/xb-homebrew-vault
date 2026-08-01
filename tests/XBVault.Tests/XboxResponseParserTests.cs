using System.IO.Compression;
using System.Net;

namespace XBVault.Tests;

public class XboxResponseParserTests : IDisposable
{
    private readonly string _dir;

    public XboxResponseParserTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string WriteZip(string manifestXml)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.msix");
        using (var fs = File.Create(path))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("AppxManifest.xml");
            using var sw = new StreamWriter(entry.Open());
            sw.Write(manifestXml);
        }
        return path;
    }

    // ---- TryParseError ----

    [Fact]
    public void TryParseError_ExtractsErrorMessage()
    {
        Assert.Equal("boom", XboxResponseParser.TryParseError("""{"ErrorMessage":"boom"}"""));
    }

    [Fact]
    public void TryParseError_NoErrorMessage_ReturnsNull()
    {
        Assert.Null(XboxResponseParser.TryParseError("""{"Code":5}"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("plain text")]
    public void TryParseError_InvalidInput_ReturnsNull(string? body)
    {
        Assert.Null(XboxResponseParser.TryParseError(body));
    }

    // ---- ParseMsixPackageName ----

    [Fact]
    public void ParseMsixPackageName_ExtractsIdentityName()
    {
        var path = WriteZip(
            """<Package><Identity Name="MyGame.Package" Publisher="CN=X" Version="1.0.0.0"/></Package>""");

        Assert.Equal("MyGame.Package", XboxResponseParser.ParseMsixPackageName(path));
    }

    [Fact]
    public void ParseMsixPackageName_MissingManifest_ReturnsNull()
    {
        var path = Path.Combine(_dir, "no-manifest.msix");
        using (var fs = File.Create(path))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("OtherFile.txt");
            using var sw = new StreamWriter(entry.Open());
            sw.Write("x");
        }

        Assert.Null(XboxResponseParser.ParseMsixPackageName(path));
    }

    [Fact]
    public void ParseMsixPackageName_ManifestWithoutIdentity_ReturnsNull()
    {
        var path = WriteZip("""<Package><Properties><DisplayName>X</DisplayName></Properties></Package>""");

        Assert.Null(XboxResponseParser.ParseMsixPackageName(path));
    }

    [Fact]
    public void ParseMsixPackageName_NotAZip_ReturnsNull()
    {
        var path = Path.Combine(_dir, "garbage.msix");
        File.WriteAllText(path, "this is not a zip");

        Assert.Null(XboxResponseParser.ParseMsixPackageName(path));
    }

    [Fact]
    public void ParseMsixPackageName_MissingFile_ReturnsNull()
    {
        Assert.Null(XboxResponseParser.ParseMsixPackageName(Path.Combine(_dir, "nope.msix")));
    }

    // ---- IsIdleCode ----

    [Theory]
    [InlineData(HttpStatusCode.NotFound, true)]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public void IsIdleCode_DetectsIdleStatuses(HttpStatusCode code, bool expected)
    {
        Assert.Equal(expected, XboxResponseParser.IsIdleCode(code));
    }

    // ---- Error classification helpers ----

    [Fact]
    public void IsSignatureError_DetectsSignatureCode()
    {
        var code = unchecked((int)0x800B0100);
        Assert.True(XboxResponseParser.IsSignatureError($$"""{"Code":{{code}}}"""));
        Assert.False(XboxResponseParser.IsSignatureError("""{"Code":123}"""));
        Assert.False(XboxResponseParser.IsSignatureError("not json"));
    }

    [Fact]
    public void IsResourceInUseError_DetectsAndExtractsReason()
    {
        var code = unchecked((int)0x80073D02);
        Assert.True(XboxResponseParser.IsResourceInUseError(
            $$"""{"Code":{{code}},"Reason":"Game running"}""", out var apps));
        Assert.Equal("Game running", apps);

        Assert.False(XboxResponseParser.IsResourceInUseError("""{"Code":99}""", out _));
        Assert.False(XboxResponseParser.IsResourceInUseError("garbage", out _));
    }

    [Fact]
    public void IsHigherVersionError_DetectsAndExtractsReason()
    {
        var code = unchecked((int)0x80070490);
        Assert.True(XboxResponseParser.IsHigherVersionError(
            $$"""{"Code":{{code}},"Reason":"Newer version installed"}""", out var msg));
        Assert.Equal("Newer version installed", msg);

        Assert.False(XboxResponseParser.IsHigherVersionError("""{"Code":7}""", out _));
    }

    [Fact]
    public void IsFatalDeploymentError_DetectsAndBuildsMessage()
    {
        var code = unchecked((int)0x80073D0D);
        Assert.True(XboxResponseParser.IsFatalDeploymentError(
            $$"""{"Success":false,"Code":{{code}},"Reason":"Deploy failed"}""", out var err));
        Assert.Equal($"Code={code} Deploy failed", err);

        Assert.True(XboxResponseParser.IsFatalDeploymentError(
            """{"Success":false}""", out var errNoReason));
        Assert.Equal("Code=0 ", errNoReason);

        Assert.False(XboxResponseParser.IsFatalDeploymentError("""{"Success":true}""", out _));
        Assert.False(XboxResponseParser.IsFatalDeploymentError("garbage", out _));
    }

    // ---- IsJsonSuccess ----

    [Fact]
    public void IsJsonSuccess_TrueWithCodeTextAndReason()
    {
        var ok = XboxResponseParser.IsJsonSuccess(
            """{"Success":true,"CodeText":"OK","Reason":"Installed"}""", out var msg);

        Assert.True(ok);
        Assert.Equal("Installed OK", msg);
    }

    [Fact]
    public void IsJsonSuccess_TrueNoCodeText_UsesReasonOnly()
    {
        var ok = XboxResponseParser.IsJsonSuccess(
            """{"Success":true,"Reason":"Done"}""", out var msg);

        Assert.True(ok);
        Assert.Equal("Done", msg);
    }

    [Fact]
    public void IsJsonSuccess_False_BuildsErrorSummary()
    {
        var code = unchecked((int)0x80073D02);
        var ok = XboxResponseParser.IsJsonSuccess(
            $$"""{"Success":false,"Code":{{code}},"CodeText":"IN_USE","Reason":"Busy"}""", out var msg);

        Assert.False(ok);
        Assert.Equal($"Code={code} Reason=Busy CodeText=IN_USE", msg);
    }

    [Fact]
    public void IsJsonSuccess_MissingSuccessField_IsFalse()
    {
        Assert.False(XboxResponseParser.IsJsonSuccess("""{"Status":"running"}""", out var msg));
        Assert.StartsWith("Code=-1", msg);
    }

    [Fact]
    public void IsJsonSuccess_InvalidJson_IsFalse()
    {
        Assert.False(XboxResponseParser.IsJsonSuccess("not json", out var msg));
        Assert.StartsWith("Parse error:", msg);
    }

    // ---- Truncate ----

    [Theory]
    [InlineData("short", 100, "short")]
    [InlineData("exactly-five", 12, "exactly-five")]
    [InlineData("abcdefghij", 5, "abcde... (truncated)")]
    [InlineData("", 5, "")]
    public void Truncate_ShortensWhenNeeded(string input, int max, string expected)
    {
        Assert.Equal(expected, XboxResponseParser.Truncate(input, max));
    }

    // ---- SizeFormat ----

    [Theory]
    [InlineData(0, "0.0B")]
    [InlineData(500, "500.0B")]
    [InlineData(1024, "1.0KB")]
    [InlineData(1536, "1.5KB")]
    [InlineData(1024 * 1024, "1.0MB")]
    [InlineData(1024L * 1024 * 1024, "1.0GB")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1.0TB")]
    [InlineData(2048L * 1024 * 1024 * 1024, "2.0TB")]
    public void SizeFormat_FormatsUnits(long bytes, string expected)
    {
        Assert.Equal(expected, XboxResponseParser.SizeFormat(bytes));
    }
}
