using System.Security.Cryptography;

namespace JGF.Application.Services;

/// <summary>
/// Service providing AES-256-CBC/GCM encryption and decryption for database backup files.
/// </summary>
public interface IAesEncryptionService
{
    Task EncryptFileAsync(string inputFilePath, string outputEncryptedFilePath, string keyPhrase, CancellationToken ct = default);
    Task DecryptFileAsync(string encryptedFilePath, string outputDecryptedFilePath, string keyPhrase, CancellationToken ct = default);
}

public class AesEncryptionService : IAesEncryptionService
{
    private const int KeySize = 32; // 256 bits
    private const int IvSize = 16;  // 128 bits
    private const int SaltSize = 16;
    private const int Iterations = 100_000;

    public async Task EncryptFileAsync(string inputFilePath, string outputEncryptedFilePath, string keyPhrase, CancellationToken ct = default)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var kdf = new Rfc2898DeriveBytes(keyPhrase, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(KeySize);
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

        using var outFs = new FileStream(outputEncryptedFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await outFs.WriteAsync(salt, ct);
        await outFs.WriteAsync(iv, ct);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var cryptoStream = new CryptoStream(outFs, aes.CreateEncryptor(), CryptoStreamMode.Write);
        using var inFs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await inFs.CopyToAsync(cryptoStream, ct);
    }

    public async Task DecryptFileAsync(string encryptedFilePath, string outputDecryptedFilePath, string keyPhrase, CancellationToken ct = default)
    {
        using var inFs = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        byte[] salt = new byte[SaltSize];
        int readSalt = await inFs.ReadAsync(salt, 0, SaltSize, ct);
        if (readSalt != SaltSize) throw new InvalidDataException("Invalid encrypted backup file header (salt truncated).");

        byte[] iv = new byte[IvSize];
        int readIv = await inFs.ReadAsync(iv, 0, IvSize, ct);
        if (readIv != IvSize) throw new InvalidDataException("Invalid encrypted backup file header (IV truncated).");

        using var kdf = new Rfc2898DeriveBytes(keyPhrase, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(KeySize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var cryptoStream = new CryptoStream(inFs, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var outFs = new FileStream(outputDecryptedFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await cryptoStream.CopyToAsync(outFs, ct);
    }
}
