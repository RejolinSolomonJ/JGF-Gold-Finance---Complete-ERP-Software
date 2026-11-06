namespace JGF.Domain.Enums;

/// <summary>
/// Status of a database backup operation.
/// </summary>
public enum BackupStatus
{
    /// <summary>Backup completed successfully.</summary>
    Success,

    /// <summary>Backup failed with an error.</summary>
    Failed,

    /// <summary>Backup is currently in progress.</summary>
    InProgress
}
