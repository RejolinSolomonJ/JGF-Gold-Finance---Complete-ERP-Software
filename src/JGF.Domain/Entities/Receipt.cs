using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents a payment receipt issued when a customer makes a payment against a loan.
/// Tracks interest, principal, penalties, and other charges collected.
/// </summary>
public class Receipt : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Unique receipt number in the format "R/{FinancialYear}/{Sequence}"
    /// (e.g., "R/2024-25/00001").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the loan this receipt is against.
    /// </summary>
    public Guid LoanId { get; set; }

    /// <summary>
    /// Date on which the receipt was issued.
    /// </summary>
    public DateOnly ReceiptDate { get; set; }

    /// <summary>
    /// Type of receipt (interest-only, part-principal, closure, etc.).
    /// </summary>
    [Required]
    [MaxLength(30)]
    public ReceiptType ReceiptType { get; set; }

    /// <summary>
    /// Interest amount collected (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal InterestAmount { get; set; }

    /// <summary>
    /// Penal interest amount collected for overdue period (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PenalInterestAmount { get; set; }

    /// <summary>
    /// Principal amount repaid (in ₹). Zero for interest-only receipts.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrincipalAmount { get; set; }

    /// <summary>
    /// Other miscellaneous charges collected (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherCharges { get; set; }

    /// <summary>
    /// Description of other charges (e.g., "Document charge", "Appraisal fee").
    /// </summary>
    [MaxLength(200)]
    public string? OtherChargesDescription { get; set; }

    /// <summary>
    /// Postal charges collected for sending notices (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PostalCharges { get; set; }

    /// <summary>
    /// Total amount collected = Interest + PenalInterest + Principal + OtherCharges + PostalCharges (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Mode of payment (Cash, UPI, Bank Transfer, Cheque, Adjustment).
    /// </summary>
    [Required]
    [MaxLength(30)]
    public PaymentMode PaymentMode { get; set; }

    /// <summary>
    /// Reference number for non-cash payments (cheque number, UPI transaction ID, etc.).
    /// </summary>
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// Start date from which interest was calculated for this receipt.
    /// </summary>
    public DateOnly InterestCalculatedFromDate { get; set; }

    /// <summary>
    /// End date up to which interest was calculated for this receipt.
    /// </summary>
    public DateOnly InterestCalculatedToDate { get; set; }

    /// <summary>
    /// Number of days for which interest was charged.
    /// </summary>
    public int DaysCharged { get; set; }

    /// <summary>
    /// Foreign key to the accounting voucher generated for this receipt.
    /// </summary>
    public Guid VoucherId { get; set; }

    /// <summary>
    /// Free-text notes about this receipt.
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
    /// The loan this receipt is against.
    /// </summary>
    public Loan Loan { get; set; } = null!;

    /// <summary>
    /// The accounting voucher generated for this receipt.
    /// </summary>
    public Voucher Voucher { get; set; } = null!;
}
