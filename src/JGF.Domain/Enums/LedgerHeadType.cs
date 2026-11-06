namespace JGF.Domain.Enums;

/// <summary>
/// Classification of an accounting ledger head as per Indian double-entry bookkeeping.
/// </summary>
public enum LedgerHeadType
{
    /// <summary>Asset accounts (e.g., Cash, Bank, Gold Stock, Loans Receivable).</summary>
    Asset,

    /// <summary>Liability accounts (e.g., Bank Borrowings, Customer Deposits).</summary>
    Liability,

    /// <summary>Income accounts (e.g., Interest Income, Processing Fees).</summary>
    Income,

    /// <summary>Expense accounts (e.g., Rent, Salaries, Bank Interest Paid).</summary>
    Expense,

    /// <summary>Capital/equity accounts (e.g., Owner's Capital, Retained Earnings).</summary>
    Capital
}
