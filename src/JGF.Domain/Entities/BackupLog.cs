using System.ComponentModel.DataAnnotations;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Execution history log of database backup and restore operations.
/// </summary>
public class BackupLog : AuditableEntity
{
    public DateTime BackupDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [Required]
    public BackupType BackupType { get; set; } = BackupType.Manual;

    [Required]
    public BackupStatus Status { get; set; } = BackupStatus.InProgress;

    public string? ErrorMessage { get; set; }

    public Guid? InitiatedByUserId { get; set; }

    public bool IsEncrypted { get; set; } = true;

    [MaxLength(500)]
    public string? CloudUploadStatus { get; set; }

    [MaxLength(500)]
    public string? LanCopyStatus { get; set; }
}
