using System;
using System.Text;

namespace XBVault.Services;

public static class CryptoService
{
    private static readonly byte[] Salt = [0x58, 0x42, 0x56, 0x61, 0x75, 0x6C, 0x74, 0x21];

    public static string Obfuscate(string plainText)
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

    public static string Deobfuscate(string obfuscated)
    {
        if (string.IsNullOrEmpty(obfuscated))
            return string.Empty;

        try
        {
            var salted = Convert.FromBase64String(obfuscated);
            for (int i = 0; i < salted.Length; i++)
                salted[i] ^= Salt[i % Salt.Length];

            var bytes = new byte[salted.Length - Salt.Length];
            Array.Copy(salted, Salt.Length, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            Logger.Warn($"CryptoService: decryption failed: {ex.Message}");
            return string.Empty;
        }
    }
}
