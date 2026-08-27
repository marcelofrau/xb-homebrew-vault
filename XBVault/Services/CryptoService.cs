using System;
using System.Security.Cryptography;
using System.Text;

namespace XBVault.Services;

/// <summary>
/// Strong-at-rest encryption for settings credentials (e.g. the Xbox password).
/// AES-256-GCM with a key derived from the machine+user identity via PBKDF2 —
/// pure managed, cross-platform, no OS keychain, no new UX. A config file
/// copied to another machine/user cannot be decrypted (the key differs);
/// on failure we return false so the UI can offer the setup wizard.
///
/// Format (versioned by prefix):
///   SEC2: base64( salt(16) | nonce(12) | tag(16) | ciphertext )
/// Prefixed values from other machines fail authentication cleanly (no throw).
/// Values written by older builds (legacy XOR+salt) are still decrypted on read
/// (grandfathered) and are re-written in SEC2 format on the next credentials
/// save — existing installs keep working, nothing is lost mid-migration.
/// </summary>
public static class CryptoService
{
    private const string Prefix = "SEC2:";

    private const int SaltBytes = 16;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int Iterations = 100_000;

    private static readonly byte[] Salt = [0x58, 0x42, 0x56, 0x61, 0x75, 0x6C, 0x74, 0x21];

    public static string MachineIdentity => $"{Environment.MachineName}|{Environment.UserName}";

    public static string Obfuscate(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            return EncryptWithIdentity(MachineIdentity, plainText);
        }
        catch (Exception ex)
        {
            Logger.Warn($"CryptoService: SEC2 encrypt failed ({ex.Message}) — falling back to legacy XOR");
            return LegacyXorObfuscate(plainText);
        }
    }

    public static string Deobfuscate(string obfuscated)
        => TryDeobfuscate(obfuscated, out var value) ? value ?? string.Empty : string.Empty;

    /// <summary>
    /// Distinguishes "no stored secret" (true, value "") from "stored but
    /// undecryptable" (false) — e.g. config copied from another machine/user
    /// or a corrupted value.
    /// </summary>
    public static bool TryDeobfuscate(string? obfuscated, out string? value)
    {
        if (string.IsNullOrEmpty(obfuscated))
        {
            value = string.Empty;
            return true;
        }

        if (obfuscated.StartsWith(Prefix, StringComparison.Ordinal))
        {
            try
            {
                value = DecryptWithIdentity(MachineIdentity, obfuscated);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn($"CryptoService: stored value cannot be decrypted on this machine/user ({ex.Message})");
                value = null;
                return false;
            }
        }

        // Legacy XOR+salt value written by pre-SEC2 builds — grandfathered read.
        // It is re-encrypted to SEC2 on the next credentials save.
        try
        {
            value = LegacyXorDeobfuscate(obfuscated);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"CryptoService: stored value is corrupt ({ex.Message})");
            value = null;
            return false;
        }
    }

    /// <summary>Explicit-identity encrypt — lets tests prove cross-machine failure.</summary>
    internal static string EncryptWithIdentity(string identity, string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = DeriveKey(identity, salt);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagBytes];

        using (var gcm = new AesGcm(key, TagBytes))
            gcm.Encrypt(nonce, plain, cipher, tag);

        var blob = new byte[SaltBytes + NonceBytes + TagBytes + cipher.Length];
        salt.CopyTo(blob, 0);
        nonce.CopyTo(blob, SaltBytes);
        tag.CopyTo(blob, SaltBytes + NonceBytes);
        cipher.CopyTo(blob, SaltBytes + NonceBytes + TagBytes);

        return Prefix + Convert.ToBase64String(blob);
    }

    /// <summary>Explicit-identity decrypt. Throws when identity differs or data is corrupt/tampered.</summary>
    internal static string DecryptWithIdentity(string identity, string token)
    {
        if (string.IsNullOrEmpty(token))
            return string.Empty;

        var blob = Convert.FromBase64String(token[Prefix.Length..]);
        if (blob.Length < SaltBytes + NonceBytes + TagBytes)
            throw new CryptographicException("SEC2 blob too short");

        var salt = blob.AsSpan(0, SaltBytes).ToArray();
        var nonce = blob.AsSpan(SaltBytes, NonceBytes).ToArray();
        var tag = blob.AsSpan(SaltBytes + NonceBytes, TagBytes).ToArray();
        var cipher = blob.AsSpan(SaltBytes + NonceBytes + TagBytes).ToArray();

        var key = DeriveKey(identity, salt);
        var plain = new byte[cipher.Length];
        using (var gcm = new AesGcm(key, TagBytes))
            gcm.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    internal static byte[] DeriveKey(string identity, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(identity), salt, Iterations, HashAlgorithmName.SHA256, 32);

    /// <summary>Legacy XOR+salt obfuscation — fixture builder for migration tests.</summary>
    internal static string LegacyXorObfuscate(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var salted = new byte[bytes.Length + Salt.Length];
        Salt.CopyTo(salted, 0);
        bytes.CopyTo(salted, Salt.Length);

        for (int i = 0; i < salted.Length; i++)
            salted[i] ^= Salt[i % Salt.Length];

        return Convert.ToBase64String(salted);
    }

    private static string LegacyXorDeobfuscate(string obfuscated)
    {
        var salted = Convert.FromBase64String(obfuscated);
        for (int i = 0; i < salted.Length; i++)
            salted[i] ^= Salt[i % Salt.Length];

        var bytes = new byte[salted.Length - Salt.Length];
        Array.Copy(salted, Salt.Length, bytes, 0, bytes.Length);
        return Encoding.UTF8.GetString(bytes);
    }
}
