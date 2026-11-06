namespace JGF.Domain.Enums;

/// <summary>
/// Type of auto-incrementing number series used in the application.
/// </summary>
public enum NumberSeriesType
{
    /// <summary>Loan number series (per scheme, per financial year).</summary>
    LoanNumber,

    /// <summary>Receipt number series (per financial year).</summary>
    ReceiptNumber,

    /// <summary>Voucher number series (per financial year).</summary>
    VoucherNumber,

    /// <summary>Customer code series (global).</summary>
    CustomerCode
}
