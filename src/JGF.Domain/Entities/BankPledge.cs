using System.ComponentModel.DataAnnotations;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Bank repledge header (bulk pledges of customer gold to banks for refinance).
/// </summary>
public class BankPledge : AuditableEntity, ISoftDeletable
{
    public Guid BankId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PledgeNumber { get; set; } = string.Empty;

    public DateOnly PledgeDate { get; set; }

    public decimal TotalPledgeAmount { get; set; }

    public decimal DisbursedAmount { get; set; }

    public decimal InterestRate { get; set; }

    /// <summary>
    /// Total gold weight appraised and accepted by the bank (in grams).
    /// </summary>
    public decimal BankWeight { get; set; }

    /// <summary>
    /// Bank's official loan account number or pawn ticket number.
    /// </summary>
    [MaxLength(50)]
    public string? BankAccountOrLoanNumber { get; set; }

    /// <summary>
    /// Name of the account holder under which the bank pledge was made.
    /// </summary>
    [MaxLength(200)]
    public string? AccountHolderName { get; set; }

    /// <summary>
    /// External bank receipt number if issued.
    /// </summary>
    [MaxLength(50)]
    public string? BankReceiptNumber { get; set; }

    public DateOnly? MaturityDate { get; set; }

    [Required]
    public BankPledgeStatus Status { get; set; } = BankPledgeStatus.Active;

    [MaxLength(1000)]
    public string? Remarks { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Bank Bank { get; set; } = null!;
    public ICollection<BankPledgeLoan> BankPledgeLoans { get; set; } = new List<BankPledgeLoan>();
}

