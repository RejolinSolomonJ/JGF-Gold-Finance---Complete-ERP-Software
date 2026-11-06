using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Services;

public record BackupResult(bool Success, string FilePath, long FileSize, string? ErrorMessage = null);

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(string encryptionKey, BackupType type = BackupType.Manual, Guid? userId = null, CancellationToken ct = default);
    Task<bool> RestoreBackupAsync(string encryptedBackupPath, string encryptionKey, string targetDatabaseName, CancellationToken ct = default);
    Task<bool> HasRecentSuccessfulBackupAsync(TimeSpan threshold, CancellationToken ct = default);
    Task<List<BackupLog>> GetBackupLogsAsync(int limit = 50, CancellationToken ct = default);
}

