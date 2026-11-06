namespace JGF.Domain.Enums;

/// <summary>
/// Status of a bank repledge (bulk pledge of customer jewels to a bank).
/// </summary>
public enum BankPledgeStatus
{
    /// <summary>Pledge is currently active with the bank.</summary>
    Active,

    /// <summary>Some loans/jewels have been released from the pledge.</summary>
    PartiallyReleased,

    /// <summary>All loans/jewels have been fully released from the bank.</summary>
    FullyReleased,

    /// <summary>The bank pledge has exceeded its maturity date.</summary>
    Overdue
}
