using XBVault.Services;

namespace XBVault.Tests;

public class CryptoServiceTests
{
    [Fact]
    public void Obfuscate_Deobfuscate_Roundtrip()
    {
        var original = "MyS3cretP@ss!";
        var obfuscated = CryptoService.Obfuscate(original);
        Assert.NotEqual(original, obfuscated);
        Assert.Equal(original, CryptoService.Deobfuscate(obfuscated));
    }

    [Fact]
    public void Obfuscate_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CryptoService.Obfuscate(""));
    }

    [Fact]
    public void Deobfuscate_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CryptoService.Deobfuscate(""));
    }

    [Fact]
    public void Deobfuscate_NullString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CryptoService.Deobfuscate(null!));
    }

    [Fact]
    public void Obfuscate_NullString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CryptoService.Obfuscate(null!));
    }

    [Fact]
    public void Deobfuscate_CorruptString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CryptoService.Deobfuscate("not-valid-base64!!!"));
    }

    [Fact]
    public void Obfuscate_DifferentInputs_ProduceDifferentOutputs()
    {
        var a = CryptoService.Obfuscate("password1");
        var b = CryptoService.Obfuscate("password2");
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("xbox")]
    [InlineData("192.168.1.1")]
    [InlineData("")]
    [InlineData("special chars !@#$%^&*()")]
    public void Roundtrip_VariousInputs(string input)
    {
        var result = CryptoService.Deobfuscate(CryptoService.Obfuscate(input));
        Assert.Equal(input, result);
    }

    [Fact]
    public void Obfuscate_ProducesBase64Output()
    {
        var result = CryptoService.Obfuscate("test");
        // Should not throw when converting from base64
        var bytes = Convert.FromBase64String(result);
        Assert.NotEmpty(bytes);
    }
}
