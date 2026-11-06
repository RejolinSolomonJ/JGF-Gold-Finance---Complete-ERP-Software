namespace JGF.Domain.Enums;

/// <summary>
/// Type of accounting voucher.
/// </summary>
public enum VoucherType
{
    /// <summary>Money received (e.g., loan repayment from customer).</summary>
    Receipt,

    /// <summary>Money paid out (e.g., loan disbursement, bank payment).</summary>
    Payment,

    /// <summary>Non-cash adjustment between ledger heads.</summary>
    Journal,

    /// <summary>Cash-to-bank or bank-to-cash transfer.</summary>
    Contra
}
