namespace JGF.Application.Loans;

/// <summary>
/// Breakdown line for a specific interest calculation period.
/// </summary>
public record InterestPeriodBreakdown(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal Principal,
    int Days,
    decimal Months,
    decimal MonthlyRate,
    decimal InterestAmount,
    bool IsPenal,
    string Description
);

/// <summary>
/// Result of interest calculation up to a specified date.
/// </summary>
public record InterestCalculationResult(
    DateOnly AsOfDate,
    decimal StartingPrincipal,
    decimal CurrentPrincipal,
    int TotalDays,
    decimal TotalMonthsCharged,
    decimal RegularInterest,
    decimal PenalInterest,
    decimal TotalInterest,
    decimal TotalOutstandingAmount,
    List<InterestPeriodBreakdown> Breakdown
);

/// <summary>
/// Contract for jewel loan interest calculation engines.
/// </summary>
public interface IInterestCalculator
{
    InterestCalculationResult CalculateInterest(
        decimal principal,
        decimal monthlyInterestRate,
        decimal monthlyPenalRate,
        DateOnly pledgeDate,
        DateOnly calculateUpToDate,
        DateOnly? lastInterestPaidDate = null,
        int tenureMonths = 12,
        int gracePeriodDays = 7,
        int minimumInterestDays = 30,
        bool roundUpToNextMonth = false);
}

