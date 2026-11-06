namespace JGF.Domain.Enums;

/// <summary>
/// Type of entry in the loan ledger (double-entry style tracking per loan).
/// </summary>
public enum LoanLedgerEntryType
{
    /// <summary>Initial loan disbursement to the customer.</summary>
    Disbursement,

    /// <summary>Periodic interest accrual entry.</summary>
    InterestAccrual,

    /// <summary>Interest payment received from the customer.</summary>
    InterestPayment,

    /// <summary>Principal repayment received from the customer.</summary>
    PrincipalPayment,

    /// <summary>Penal interest charged for overdue period.</summary>
    PenalInterest,

    /// <summary>Additional charges (postal, processing, etc.).</summary>
    Charge,

    /// <summary>Manual adjustment entry.</summary>
    Adjustment,

    /// <summary>Loan closure entry.</summary>
    Closure
}
