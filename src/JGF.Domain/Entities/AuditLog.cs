using System.ComponentModel.DataAnnotations;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Immutable audit log trail capturing every create, update, delete, login and critical action.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string TableName { get; set; } = string.Empty;

    public Guid? RecordId { get; set; }

    [Required]
    public AuditAction Action { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid? UserId { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    [MaxLength(500)]
    public string? AffectedColumns { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }
}

