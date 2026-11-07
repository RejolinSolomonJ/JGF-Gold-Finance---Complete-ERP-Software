using JGF.Application.Accounting;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Application.Updates;

public enum VersionComparisonResult
{
    UpToDate,
    AppOlderThanDb,
    AppNewerThanDb
}

public record VersionStatusDto(
    SemanticVersion CurrentAppVersion,
    SemanticVersion DatabaseVersion,
    VersionComparisonResult Status,
    bool CanCashierLogin,
    bool CanOwnerManagerUpgrade,
    bool HasPendingMigrations,
    string Message
);

public record UpdatePreconditionCheckResult(
    bool CanProceed,
    bool IsRunningOnServerPC1,
    bool IsDayClosed,
    bool IsPC2UserActive,
    string? ActivePC2User,
    List<string> BlockingReasons
);

public record PreUpdateBackupResult(
    bool Success,
    string BackupFilePath,
    long BackupFileSizeBytes,
    string? LanBackupPath,
    string? CloudBackupPath,
    bool SchemaVerified,
    string? ErrorMessage = null
);

public record UpdateExecutionResult(
    bool Success,
    string TargetVersion,
    string AppliedBy,
    long ElapsedMilliseconds,
    string? BackupPath,
    AccountingIntegrityCheckDto? IntegrityAuditResult,
    string? ErrorMessage = null
);

public record SchemaVersionDto(
    int Id,
    string AppVersion,
    string AppliedMigrations,
    DateTime AppliedAtUtc,
    string AppliedBy,
    bool IsSuccess,
    long ExecutionTimeMs,
    string? PreUpdateBackupPath,
    string? IntegrityCheckStatus,
    string? Notes
);

public interface ISafeUpdateService
{
    /// <summary>
    /// Checks database schema version against the executing application's semantic version.
    /// Determines whether Cashiers can log in or if Owner/Manager upgrade is required.
    /// </summary>
    Task<VersionStatusDto> CheckVersionStatusAsync(string? currentAppVersionOverride = null, CancellationToken ct = default);

    /// <summary>
    /// Validates all safety rules before update:
    /// 1. Must run on PC1 (database host).
    /// 2. Day must be closed (no unposted transactions).
    /// 3. PC2 client counter must be logged out.
    /// </summary>
    Task<UpdatePreconditionCheckResult> ValidatePreconditionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes a mandatory pre-update encrypted backup, verifies file size > 0,
    /// verifies schema test-restore, copies to LAN and cloud locations, and logs to BackupLog.
    /// If verification fails, the update MUST be aborted with no option to skip.
    /// </summary>
    Task<PreUpdateBackupResult> CreateAndVerifyPreUpdateBackupAsync(string encryptionKey, Guid? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Applies database migrations inside a transactional boundary (on PC1 only).
    /// Enforces pre-update backup, records SchemaVersion, and executes Accounts Integrity Audit.
    /// </summary>
    Task<UpdateExecutionResult> ExecuteUpdateAsync(string targetVersion, string appliedByUser, string encryptionKey, Guid? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all historical schema version entries for the About & Update History view.
    /// </summary>
    Task<List<SchemaVersionDto>> GetUpdateHistoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the heartbeat/session marker for the current station (PC1 or PC2).
    /// </summary>
    Task RecordWorkstationHeartbeatAsync(string workstationName, string username, CancellationToken ct = default);

    /// <summary>
    /// Clears workstation session marker upon logout.
    /// </summary>
    Task ClearWorkstationHeartbeatAsync(string workstationName, CancellationToken ct = default);
}
