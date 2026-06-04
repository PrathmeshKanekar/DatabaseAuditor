namespace DatabaseAuditor.Infrastructure.Persistence;

using DatabaseAuditor.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService()
    {
        // Derive key and IV from machine-specific entropy
        var entropy = GetMachineEntropy();
        using var deriveBytes = new Rfc2898DeriveBytes(
            entropy,
            Encoding.UTF8.GetBytes("DatabaseAuditor_Salt_v1"),
            100_000,
            HashAlgorithmName.SHA256);

        _key = deriveBytes.GetBytes(32); // 256-bit key
        _iv = deriveBytes.GetBytes(16); // 128-bit IV
    }

    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);
        var encrypted = EncryptBytes(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToBase64String(encrypted);
    }

    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);
        var decrypted = DecryptBytes(Convert.FromBase64String(cipherText));
        return Encoding.UTF8.GetString(decrypted);
    }

    public byte[] EncryptBytes(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }

    public byte[] DecryptBytes(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, data.Length);
    }

    private static string GetMachineEntropy()
    {
        var machineName = Environment.MachineName;
        var userName = Environment.UserName;
        var osVersion = Environment.OSVersion.VersionString;
        return $"{machineName}|{userName}|{osVersion}|DatabaseAuditor";
    }
}