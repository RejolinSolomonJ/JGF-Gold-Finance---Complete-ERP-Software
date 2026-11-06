namespace JGF.Domain.Enums;

/// <summary>
/// Status of an individual loan within a bank pledge.
/// </summary>
public enum BankPledgeLoanStatus
{
    /// <summary>The loan's jewels are currently pledged to the bank.</summary>
    Pledged,

    /// <summary>The loan's jewels have been released from the bank pledge.</summary>
    Released
}
