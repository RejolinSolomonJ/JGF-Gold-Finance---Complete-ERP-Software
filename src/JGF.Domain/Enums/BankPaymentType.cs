namespace JGF.Domain.Enums;

/// <summary>
/// Type of payment made to a bank against a repledge.
/// </summary>
public enum BankPaymentType
{
    /// <summary>Interest payment to the bank.</summary>
    InterestPayment,

    /// <summary>Partial release payment to retrieve some jewels.</summary>
    PartRelease,

    /// <summary>Full release payment to close the bank pledge.</summary>
    FullRelease,

    /// <summary>Penalty charged by the bank for overdue pledge.</summary>
    Penalty
}
