namespace JGF.Application.Loans;

/// <summary>
/// Standard Gold Loan interest calculation engine adhering to Tamil Nadu pawn broker conventions.
/// Handles simple interest by days, minimum 1-month rule, tenure maturity, grace periods, and penal rates.
/// </summary>
public class StandardInterestCalculator : IInterestCalculator
{
    public InterestCalculationResult CalculateInterest(
        decimal principal,
        decimal monthlyInterestRate,
        decimal monthlyPenalRate,
        DateOnly pledgeDate,
        DateOnly calculateUpToDate,
        DateOnly? lastInterestPaidDate = null,
        int tenureMonths = 12,
        int gracePeriodDays = 7,
        int minimumInterestDays = 30,
        bool roundUpToNextMonth = false)
    {
        if (principal <= 0)
        {
            return new InterestCalculationResult(
                calculateUpToDate, principal, principal, 0, 0, 0, 0, 0, 0, new List<InterestPeriodBreakdown>());
        }

        var startDate = lastInterestPaidDate ?? pledgeDate;
        if (calculateUpToDate <= startDate)
        {
            return new InterestCalculationResult(
                calculateUpToDate, principal, principal, 0, 0, 0, 0, 0, principal, new List<InterestPeriodBreakdown>());
        }

        var breakdown = new List<InterestPeriodBreakdown>();

        // Actual calendar days between start and calculate-up-to
        var elapsedDays = calculateUpToDate.DayNumber - startDate.DayNumber;

        // Apply minimum interest period rule if this is the first payment since pledge
        var effectiveDays = elapsedDays;
        if (!lastInterestPaidDate.HasValue && elapsedDays < minimumInterestDays)
        {
            effectiveDays = minimumInterestDays;
        }

        // Calculate maturity date and grace expiry date relative to pledgeDate
        var maturityDate = pledgeDate.AddMonths(tenureMonths);
        var graceExpiryDate = maturityDate.AddDays(gracePeriodDays);

        decimal regularInterest = 0m;
        decimal penalInterest = 0m;

        // Split calculation into normal tenure and overdue penal period
        if (calculateUpToDate <= graceExpiryDate)
        {
            // All within tenure + grace period: Regular interest only
            var monthsCharged = CalculateMonthsCharged(effectiveDays, roundUpToNextMonth);
            var interest = Math.Round(principal * (monthlyInterestRate / 100m) * monthsCharged, 2, MidpointRounding.AwayFromZero);
            regularInterest += interest;

            breakdown.Add(new InterestPeriodBreakdown(
                startDate,
                calculateUpToDate,
                principal,
                effectiveDays,
                monthsCharged,
                monthlyInterestRate,
                interest,
                false,
                effectiveDays > elapsedDays 
                    ? $"Regular interest (Min {minimumInterestDays} days applied)" 
                    : "Regular interest"));
        }
        else
        {
            // Crosses past grace period: Portion up to maturity/grace is regular, excess is penal
            var regularEndDate = graceExpiryDate;
            var regularDays = Math.Max(0, regularEndDate.DayNumber - startDate.DayNumber);
            if (regularDays < minimumInterestDays && !lastInterestPaidDate.HasValue)
            {
                regularDays = minimumInterestDays;
            }

            var regularMonths = CalculateMonthsCharged(regularDays, roundUpToNextMonth);
            var regularAmt = Math.Round(principal * (monthlyInterestRate / 100m) * regularMonths, 2, MidpointRounding.AwayFromZero);
            regularInterest += regularAmt;

            breakdown.Add(new InterestPeriodBreakdown(
                startDate,
                regularEndDate,
                principal,
                regularDays,
                regularMonths,
                monthlyInterestRate,
                regularAmt,
                false,
                "Regular tenure interest"));

            // Overdue penal portion
            var penalDays = calculateUpToDate.DayNumber - regularEndDate.DayNumber;
            var penalMonths = CalculateMonthsCharged(penalDays, roundUpToNextMonth);
            
            // Penal rate is regular rate + penal surcharge rate
            var totalOverdueRate = monthlyInterestRate + monthlyPenalRate;
            var penalAmt = Math.Round(principal * (totalOverdueRate / 100m) * penalMonths, 2, MidpointRounding.AwayFromZero);
            penalInterest += penalAmt;

            breakdown.Add(new InterestPeriodBreakdown(
                regularEndDate,
                calculateUpToDate,
                principal,
                penalDays,
                penalMonths,
                totalOverdueRate,
                penalAmt,
                true,
                $"Overdue penal interest ({monthlyInterestRate}% + {monthlyPenalRate}% penal)"));
        }

        var totalInterest = regularInterest + penalInterest;
        var totalOutstanding = principal + totalInterest;

        var totalChargedMonths = breakdown.Sum(b => b.Months);

        return new InterestCalculationResult(
            calculateUpToDate,
            principal,
            principal,
            effectiveDays,
            totalChargedMonths,
            regularInterest,
            penalInterest,
            totalInterest,
            totalOutstanding,
            breakdown);
    }

    private static decimal CalculateMonthsCharged(int days, bool roundUpToNextMonth)
    {
        if (days <= 0) return 0m;

        if (roundUpToNextMonth)
        {
            // Any fraction of a 30-day block rounds up to next whole month
            return Math.Ceiling(days / 30m);
        }

        // Standard 30-day month pro-rata calculation
        return Math.Round(days / 30m, 4, MidpointRounding.AwayFromZero);
    }
}

