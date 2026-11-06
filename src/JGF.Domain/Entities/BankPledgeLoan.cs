using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Links individual customer loans to a BankPledge for refinance.
/// </summary>
public class BankPledgeLoan : AuditableEntity, ISoftDeletable
{
    public Guid BankPledgeId { get; set; }
    public Guid LoanId { get; set; }

    public decimal PledgedAmount { get; set; }

    public DateOnly PledgedDate { get; set; }

    public DateOnly? ReleasedDate { get; set; }

    public bool IsReleased { get; set; } = false;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public BankPledge BankPledge { get; set; } = null!;
    public Loan Loan { get; set; } = null!;
}
