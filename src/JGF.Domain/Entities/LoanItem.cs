using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents an individual jewellery item pledged under a loan.
/// A loan can have multiple items (e.g., 2 bangles, 1 chain).
/// </summary>
public class LoanItem : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Foreign key to the parent loan.
    /// </summary>
    public Guid LoanId { get; set; }

    /// <summary>
    /// Sequence number of this item within the loan (1, 2, 3, …).
    /// </summary>
    public int ItemNumber { get; set; }

    /// <summary>
    /// Foreign key to the type of jewel (e.g., Ring 22K, Chain 24K).
    /// </summary>
    public Guid JewelTypeId { get; set; }

    /// <summary>
    /// Description of the item in English (e.g., "Gold chain with pendant").
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Description of the item in Tamil.
    /// </summary>
    [MaxLength(300)]
    public string? DescriptionTamil { get; set; }

    /// <summary>
    /// Number of pieces (e.g., pair of earrings = 2).
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gross weight of the item before stone/enamel deduction (in grams).
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal GrossWeight { get; set; }

    /// <summary>
    /// Net weight after deduction (in grams). This is the weight used for valuation.
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal NetWeight { get; set; }

    /// <summary>
    /// Weight deducted for stones, enamel, wax, etc. (in grams).
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal DeductionWeight { get; set; }

    /// <summary>
    /// Gold purity percentage of this item (e.g., 91.67 for 22K).
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal PurityPercent { get; set; }

    /// <summary>
    /// Appraised (estimated) value of this item (in ₹).
    /// Calculated as NetWeight × GoldRate × PurityFactor.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AppraisedValue { get; set; }

    /// <summary>
    /// Free-text notes about this item.
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Soft Delete ──────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAtUtc { get; set; }

    // ── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// The loan this item is pledged under.
    /// </summary>
    public Loan Loan { get; set; } = null!;

    /// <summary>
    /// The jewel type classification of this item.
    /// </summary>
    public JewelType JewelType { get; set; } = null!;
}
