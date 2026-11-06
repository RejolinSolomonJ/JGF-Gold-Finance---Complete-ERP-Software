using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents an amount-based interest rate slab within a scheme.
/// When a scheme has slabs, the interest rate is determined by the loan amount range.
/// </summary>
public class SchemeSlab : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Foreign key to the parent scheme.
    /// </summary>
    public Guid SchemeId { get; set; }

    /// <summary>
    /// Minimum loan amount for this slab (inclusive).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal MinAmount { get; set; }

    /// <summary>
    /// Maximum loan amount for this slab (inclusive).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxAmount { get; set; }

    /// <summary>
    /// Monthly interest rate for loans falling within this slab's amount range (percentage).
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal InterestRatePerMonth { get; set; }

    // ── Soft Delete ──────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAtUtc { get; set; }

    // ── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// The scheme this slab belongs to.
    /// </summary>
    public Scheme Scheme { get; set; } = null!;
}
