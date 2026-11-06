using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents a type of jewellery item that can be pledged (e.g., "Ring 22K", "Chain 24K").
/// Used for categorising loan items and standardising descriptions.
/// </summary>
public class JewelType : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Unique code for the jewel type (e.g., "RNG22", "CHN24").
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name in English (e.g., "Ring 22K").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name in Tamil (e.g., "மோதிரம் 22K").
    /// </summary>
    [MaxLength(100)]
    public string? NameTamil { get; set; }

    /// <summary>
    /// Default gold purity percentage for this jewel type (e.g., 91.67 for 22K, 99.50 for 24K).
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal DefaultPurityPercent { get; set; }

    [NotMapped]
    public string Description
    {
        get => Name;
        set => Name = value;
    }

    [NotMapped]
    public bool IsActive
    {
        get => !IsDeleted;
        set => IsDeleted = !value;
    }

    [NotMapped]
    public decimal Purity
    {
        get => DefaultPurityPercent;
        set => DefaultPurityPercent = value;
    }

    // ── Soft Delete ──────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAtUtc { get; set; }

    // ── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// Loan items of this jewel type.
    /// </summary>
    public ICollection<LoanItem> LoanItems { get; set; } = new List<LoanItem>();
}
