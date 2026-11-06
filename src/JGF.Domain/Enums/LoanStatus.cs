namespace JGF.Domain.Enums;

/// <summary>
/// Current status of a gold loan.
/// </summary>
public enum LoanStatus
{
    /// <summary>Loan is active and outstanding.</summary>
    Active,

    /// <summary>Part of the principal has been repaid; loan is still outstanding.</summary>
    PartiallyPaid,

    /// <summary>Loan has been fully repaid and closed.</summary>
    Closed,

    /// <summary>Loan was renewed (rolled over) into a new loan.</summary>
    Renewed,

    /// <summary>Pledged jewels were auctioned to recover the outstanding amount.</summary>
    Auctioned,

    /// <summary>Loan was written off as an irrecoverable loss.</summary>
    WrittenOff
}
