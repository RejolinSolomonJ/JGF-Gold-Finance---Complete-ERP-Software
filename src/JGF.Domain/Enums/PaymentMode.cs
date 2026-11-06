namespace JGF.Domain.Enums;

/// <summary>
/// Mode of payment for receipts and bank payments.
/// </summary>
public enum PaymentMode
{
    /// <summary>Cash payment at the counter.</summary>
    Cash,

    /// <summary>Bank transfer (NEFT/RTGS/IMPS).</summary>
    BankTransfer,

    /// <summary>Unified Payments Interface (UPI) transfer.</summary>
    UPI,

    /// <summary>Payment by cheque.</summary>
    Cheque,

    /// <summary>Internal adjustment entry (no physical payment).</summary>
    Adjustment
}
