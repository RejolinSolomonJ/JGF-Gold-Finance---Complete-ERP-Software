namespace JGF.Domain.Enums;

/// <summary>
/// Type of database backup.
/// </summary>
public enum BackupType
{
    /// <summary>Full database backup.</summary>
    Full,

    /// <summary>Differential backup (changes since last full backup).</summary>
    Differential,

    /// <summary>Manually triggered backup by a user.</summary>
    Manual,

    /// <summary>Automatically scheduled backup.</summary>
    Scheduled,

    /// <summary>Mandatory automated backup taken immediately prior to applying a schema migration or software update.</summary>
    PreUpdate
}
