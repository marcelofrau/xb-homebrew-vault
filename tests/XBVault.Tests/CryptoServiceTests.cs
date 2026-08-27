using System.Security.Cryptography;
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
    public void Obfuscate_ProducesPrefixedBase64Output()
    {
        var result = CryptoService.Obfuscate("test");
        Assert.StartsWith("SEC2:", result);
        // Should not throw when converting the payload from base64
        var bytes = Convert.FromBase64String(result["SEC2:".Length..]);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Obfuscate_SameInputTwice_ProducesDifferentTokens()
    {
        var a = CryptoService.Obfuscate("same-password");
        var b = CryptoService.Obfuscate("same-password");
        Assert.NotEqual(a, b); // random salt + nonce every time
    }

    [Fact]
    public void TryDeobfuscate_EmptyInput_IsSuccessWithEmptyValue()
    {
        Assert.True(CryptoService.TryDeobfuscate("", out var value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void TryDeobfuscate_TamperedToken_IsFailure()
    {
        var token = CryptoService.Obfuscate("secret-password");
        var payload = Convert.FromBase64String(token["SEC2:".Length..]);
        payload[^1] ^= 0xFF; // flip the last ciphertext byte
        var tampered = "SEC2:" + Convert.ToBase64String(payload);

        Assert.False(CryptoService.TryDeobfuscate(tampered, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void LegacyXor_Value_IsGrandfathered_AndMigratesToSec2()
    {
        const string plain = "legacy-configured-password";

        // Value as written by pre-SEC2 builds (legacy XOR+salt format)
        var legacy = CryptoService.LegacyXorObfuscate(plain);

        Assert.True(CryptoService.TryDeobfuscate(legacy, out var decrypted));
        Assert.Equal(plain, decrypted);
        Assert.StartsWith("SEC2:", CryptoService.Obfuscate(plain)); // next save writes the new format
    }

    [Fact]
    public void CrossMachine_Token_IsNotDecryptable()
    {
        var token = CryptoService.EncryptWithIdentity("MachineA|UserA", "secret");
        Assert.ThrowsAny<CryptographicException>(() => CryptoService.DecryptWithIdentity("MachineB|UserB", token));
    }

    [Fact]
    public void CrossMachine_Token_SameIdentity_IsDecryptable()
    {
        var token = CryptoService.EncryptWithIdentity("MachineA|UserA", "secret");
        Assert.Equal("secret", CryptoService.DecryptWithIdentity("MachineA|UserA", token));
    }

    [Fact]
    public void DeriveKey_SameIdentityAndSalt_IsStable()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var a = CryptoService.DeriveKey("machine|user", salt);
        var b = CryptoService.DeriveKey("machine|user", salt);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DeriveKey_DifferentSalt_ProducesDifferentKey()
    {
        var a = CryptoService.DeriveKey("machine|user", RandomNumberGenerator.GetBytes(16));
        var b = CryptoService.DeriveKey("machine|user", RandomNumberGenerator.GetBytes(16));
        Assert.NotEqual(a, b);
    }
}
