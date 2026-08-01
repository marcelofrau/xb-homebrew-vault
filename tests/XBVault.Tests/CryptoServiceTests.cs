using System.IO.Compression;
using XBVault.Services;

namespace XBVault.Tests;

public class CryptoServiceTests
{
    [Theory]
    [InlineData("hello world")]
    [InlineData("user@example.com")]
    [InlineData("p@ssw0rd with spaces!")]
    [InlineData("Ünïcödé ✓ strings 日本語")]
    public void RoundTrip_ReturnsOriginal(string input)
    {
        var obfuscated = CryptoService.Obfuscate(input);
        var deobfuscated = CryptoService.Deobfuscate(obfuscated);

        Assert.Equal(input, deobfuscated);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Obfuscate_EmptyOrNull_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, CryptoService.Obfuscate(input!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Deobfuscate_EmptyOrNull_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, CryptoService.Deobfuscate(input!));
    }

    [Fact]
    public void Obfuscate_IsNotPlainText()
    {
        var obfuscated = CryptoService.Obfuscate("secret-value");

        Assert.NotEqual("secret-value", obfuscated);
    }

    [Fact]
    public void Obfuscate_IsDeterministic()
    {
        Assert.Equal(
            CryptoService.Obfuscate("same input"),
            CryptoService.Obfuscate("same input"));
    }

    [Theory]
    [InlineData("not-base64!!!")]
    [InlineData("$$$")]
    [InlineData("aGVsbG8=")] // valid base64 but wrong length/salt → likely fails
    public void Deobfuscate_InvalidInput_ReturnsEmpty(string input)
    {
        Assert.Equal(string.Empty, CryptoService.Deobfuscate(input));
    }

    [Fact]
    public void RoundTrip_ManyIterations_Stable()
    {
        for (int i = 0; i < 100; i++)
        {
            var input = $"credential-{i}-value";
            Assert.Equal(input, CryptoService.Deobfuscate(CryptoService.Obfuscate(input)));
        }
    }
}
