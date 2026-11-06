using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents a gold loan (pawn ticket) issued to a customer against pledged jewellery.
/// This is the core business entity of the JGF application.
/// </summary>
public class Loan : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Unique loan number in the format "{SchemeCode}/{FinancialYear}/{Sequence}"
    /// (e.g., "S1/2024-25/0001"). Unique per scheme.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string LoanNumber { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the borrowing customer.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Foreign key to the loan scheme under which this loan was issued.
    /// </summary>
    public Guid SchemeId { get; set; }

    /// <summary>
    /// Date on which the jewels were pledged and the loan was disbursed.
    /// </summary>
    public DateOnly PledgeDate { get; set; }

    /// <summary>
    /// Calculated maturity date based on scheme tenure.
    /// </summary>
    public DateOnly MaturityDate { get; set; }

    /// <summary>
    /// Total loan amount disbursed to the customer (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal LoanAmount { get; set; }

    /// <summary>
    /// Total market value of all pledged jewels (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal MarketValue { get; set; }

    /// <summary>
    /// Total net weight of all pledged jewels after deductions (in grams).
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal NetWeight { get; set; }

    /// <summary>
    /// Total gross weight of all pledged jewels before deductions (in grams).
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal GrossWeight { get; set; }

    /// <summary>
    /// Total weight deduction for stones, enamel, etc. (in grams).
    /// </summary>
    [Column(TypeName = "decimal(10,3)")]
    public decimal DeductionWeight { get; set; }

    /// <summary>
    /// Gold rate (per gram) applied on the pledge date for valuation (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ApplicableGoldRate { get; set; }

    /// <summary>
    /// Current status of the loan.
    /// </summary>
    [Required]
    [MaxLength(30)]
    public LoanStatus Status { get; set; }

    /// <summary>
    /// Date on which the loan was closed/settled. Null if still active.
    /// </summary>
    public DateOnly? ClosedDate { get; set; }

    /// <summary>
    /// Foreign key to the closing receipt. Null if not yet closed.
    /// </summary>
    public Guid? ClosingReceiptId { get; set; }

    /// <summary>
    /// If this loan was renewed, points to the new loan that replaced it.
    /// </summary>
    public Guid? RenewalLoanId { get; set; }

    /// <summary>
    /// Free-text notes about the loan.
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
    /// The customer who took this loan.
    /// </summary>
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// The scheme under which this loan was issued.
    /// </summary>
    public Scheme Scheme { get; set; } = null!;

    /// <summary>
    /// The receipt used to close this loan (if closed).
    /// </summary>
    public Receipt? ClosingReceipt { get; set; }

    /// <summary>
    /// The new loan created when this loan was renewed (if applicable).
    /// </summary>
    public Loan? RenewalLoan { get; set; }

    /// <summary>
    /// Individual jewellery items pledged under this loan.
    /// </summary>
    public ICollection<LoanItem> Items { get; set; } = new List<LoanItem>();

    /// <summary>
    /// Receipts (payments) received against this loan.
    /// </summary>
    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

    /// <summary>
    /// Ledger entries tracking the financial movement of this loan.
    /// </summary>
    public ICollection<LoanLedgerEntry> LedgerEntries { get; set; } = new List<LoanLedgerEntry>();

    /// <summary>
    /// Notices sent to the customer regarding this loan.
    /// </summary>
    public ICollection<Notice> Notices { get; set; } = new List<Notice>();

    /// <summary>
    /// Auction records for this loan (if any).
    /// </summary>
    public ICollection<Auction> Auctions { get; set; } = new List<Auction>();

    /// <summary>
    /// Bank pledge linkages — tracks which bank pledges include this loan's jewels.
    /// </summary>
    public ICollection<BankPledgeLoan> BankPledgeLoans { get; set; } = new List<BankPledgeLoan>();
}
