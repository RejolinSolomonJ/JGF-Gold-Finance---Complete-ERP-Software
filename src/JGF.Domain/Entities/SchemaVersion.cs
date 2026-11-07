using System.ComponentModel.DataAnnotations;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Records application and database schema versions, EF Core migration records,
/// execution metadata, and pre-update backup references.
/// </summary>
public class SchemaVersion
{
    [Key]
    public int Id { get; set; }
    [Required]
    [MaxLength(50)]
    public string AppVersion { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string AppliedMigrations { get; set; } = string.Empty;

    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    public string AppliedBy { get; set; } = string.Empty;

    public bool IsSuccess { get; set; } = true;

    public long ExecutionTimeMs { get; set; }

    [MaxLength(500)]
    public string? PreUpdateBackupPath { get; set; }

    public long PreUpdateBackupSizeBytes { get; set; }

    [MaxLength(500)]
    public string? IntegrityCheckStatus { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
