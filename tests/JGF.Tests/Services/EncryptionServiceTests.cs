using System.Text;
using FluentAssertions;
using JGF.Application.Services;
using Xunit;

namespace JGF.Tests.Services;

public class EncryptionServiceTests
{
    private readonly AesEncryptionService _service = new();

    [Fact]
    public async Task EncryptAndDecryptFile_ShouldPreserveExactContent()
    {
        var tempPlain = Path.GetTempFileName();
        var tempEncrypted = Path.GetTempFileName();
        var tempDecrypted = Path.GetTempFileName();

        try
        {
            var originalText = "-- PostgreSQL JGF Database Dump\nCREATE TABLE Test (Id INT);\nINSERT INTO Test VALUES (1);";
            await File.WriteAllTextAsync(tempPlain, originalText, Encoding.UTF8);

            var key = "SuperSecretOwnerKey2026";

            // Encrypt
            await _service.EncryptFileAsync(tempPlain, tempEncrypted, key);
            new FileInfo(tempEncrypted).Length.Should().BeGreaterThan(0);

            // Decrypt
            await _service.DecryptFileAsync(tempEncrypted, tempDecrypted, key);
            var recoveredText = await File.ReadAllTextAsync(tempDecrypted, Encoding.UTF8);

            recoveredText.Should().Be(originalText);
        }
        finally
        {
            if (File.Exists(tempPlain)) File.Delete(tempPlain);
            if (File.Exists(tempEncrypted)) File.Delete(tempEncrypted);
            if (File.Exists(tempDecrypted)) File.Delete(tempDecrypted);
        }
    }
}

