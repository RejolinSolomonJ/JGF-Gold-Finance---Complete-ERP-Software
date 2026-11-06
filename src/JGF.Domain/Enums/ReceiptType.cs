namespace JGF.Domain.Enums;

/// <summary>
/// Type of receipt issued against a loan.
/// </summary>
public enum ReceiptType
{
    /// <summary>Interest-only payment; principal remains unchanged.</summary>
    InterestOnly,

    /// <summary>Partial principal repayment along with interest.</summary>
    PartPrincipal,

    /// <summary>Full closure of the loan (principal + all interest).</summary>
    Closure,

    /// <summary>Loan renewal — old loan closed and new loan issued.</summary>
    Renewal,

    /// <summary>Receipt against auction proceeds.</summary>
    Auction,

    /// <summary>Adjustment entry (e.g., waiver, correction).</summary>
    Adjustment
}
