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
