using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents a loan scheme / product offered by JGF.
/// Defines interest rates, tenure, LTV limits, and penalty rules.
/// </summary>
public class Scheme : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Unique scheme code (e.g., "S1", "S2"). Used in loan number prefixes.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Descriptive name of the scheme (e.g., "Regular Gold Loan", "Premium Scheme").
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Method of interest calculation — Simple or Compound.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public InterestMethod InterestMethod { get; set; }

    /// <summary>
    /// Monthly interest rate as a percentage (e.g., 1.50 means 1.5% per month).
    /// This is the default rate; may be overridden by slabs.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal InterestRatePerMonth { get; set; }

    /// <summary>
    /// Minimum number of days for which interest is charged, even if loan is closed earlier.
    /// </summary>
    public int MinimumInterestDays { get; set; }

    /// <summary>
    /// Minimum number of months for which interest is charged.
    /// </summary>
    public int MinimumInterestMonths { get; set; }

    /// <summary>
    /// Maximum Loan-to-Value percentage allowed under this scheme.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal MaxLtvPercent { get; set; }

    /// <summary>
    /// Maximum tenure in months before the loan is considered overdue.
    /// </summary>
    public int MaxTenureMonths { get; set; }

    /// <summary>
    /// Number of grace days allowed after the maturity date before penal interest kicks in.
    /// </summary>
    public int GraceDaysAfterExpiry { get; set; }

    /// <summary>
    /// Monthly penal interest rate charged on overdue loans (percentage).
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal PenalInterestRatePerMonth { get; set; }

    /// <summary>
    /// Whether the scheme is currently active and available for new loans.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Free-text notes about the scheme.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── Soft Delete ──────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAtUtc { get; set; }

    // ── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// Amount-based interest rate slabs for this scheme.
    /// </summary>
    public ICollection<SchemeSlab> Slabs { get; set; } = new List<SchemeSlab>();

    /// <summary>
    /// Loans issued under this scheme.
    /// </summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
