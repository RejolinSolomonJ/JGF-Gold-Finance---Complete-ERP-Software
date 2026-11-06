namespace JGF.Domain.Common;

/// <summary>
/// Interface for entities that support soft deletion.
/// Entities implementing this interface are never physically removed from the database.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Indicates whether the entity has been soft-deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was soft-deleted. Null if not deleted.
    /// </summary>
    DateTime? DeletedAtUtc { get; set; }
}
