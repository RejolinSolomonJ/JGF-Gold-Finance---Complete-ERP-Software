using System.ComponentModel.DataAnnotations;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// System and company configuration settings.
/// </summary>
public class Setting : AuditableEntity, ISoftDeletable
{
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(20)]
    public string DataType { get; set; } = "string";

    public bool IsEditable { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

