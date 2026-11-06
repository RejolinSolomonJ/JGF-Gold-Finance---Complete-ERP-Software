using System.ComponentModel.DataAnnotations;

namespace JGF.Domain.Common;

/// <summary>
/// Abstract base class for all auditable entities in the JGF system.
/// Provides a primary key, creation/update timestamps, and user tracking.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>
    /// Unique identifier for the entity. Auto-generated GUID.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// UTC timestamp when the record was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the record was last updated.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// The user who created this record. Nullable for system-generated records.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// The user who last updated this record. Nullable for system-generated updates.
    /// </summary>
    public Guid? UpdatedByUserId { get; set; }
}
