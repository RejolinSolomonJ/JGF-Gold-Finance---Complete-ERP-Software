using FluentAssertions;
using JGF.Application.Loans;
using Xunit;

namespace JGF.Tests.Loans;

public class InterestCalculatorTests
{
    private readonly StandardInterestCalculator _calculator = new();

    [Fact]
    public void Scenario1_MinimumOneMonthRule_ShouldCharge30DaysIfRepaidOnDay5()
    {
        // Loan of ₹76,000, pledged on 15-Jun-2024, customer pays on 20-Jun-2024 (5 days)
        var pledgeDate = new DateOnly(2024, 6, 15);
        var paymentDate = new DateOnly(2024, 6, 20);

        var result = _calculator.CalculateInterest(
            principal: 76000m,
            monthlyInterestRate: 1.5m,
            monthlyPenalRate: 0.5m,
            pledgeDate: pledgeDate,
            calculateUpToDate: paymentDate,
            minimumInterestDays: 30,
            roundUpToNextMonth: true);

        // Even though only 5 days passed, minimum 30 days (1 month) is charged: ₹76,000 * 1.5% = ₹1,140
        result.TotalDays.Should().Be(30);
        result.TotalMonthsCharged.Should().Be(1m);
        result.RegularInterest.Should().Be(1140.00m);
        result.PenalInterest.Should().Be(0m);
        result.TotalOutstandingAmount.Should().Be(77140.00m);
    }

    [Fact]
    public void Scenario2_InterestOnlyReceipt_ThreeMonthsExact()
    {
        // Loan of ₹76,000, pledged 15-Jun-2024, interest calculated up to 13-Sep-2024 (90 days = 3 months)
        var pledgeDate = new DateOnly(2024, 6, 15);
        var paymentDate = new DateOnly(2024, 9, 13);

        var result = _calculator.CalculateInterest(
            principal: 76000m,
            monthlyInterestRate: 1.5m,
            monthlyPenalRate: 0.5m,
            pledgeDate: pledgeDate,
            calculateUpToDate: paymentDate,
            roundUpToNextMonth: false);

        // 90 days / 30 = 3 months. ₹76,000 * 1.5% * 3 = ₹3,420
        result.TotalDays.Should().Be(90);
        result.TotalMonthsCharged.Should().Be(3.0m);
        result.RegularInterest.Should().Be(3420.00m);
        result.TotalInterest.Should().Be(3420.00m);
    }

    [Fact]
    public void Scenario3_PartPrincipalPayment_RecalculationFromPaymentDate()
    {
        // Loan ₹76,000. Last paid up to 13-Sep-2024. Next payment on 13-Oct-2024 (30 days = 1 month)
        var lastPaid = new DateOnly(2024, 9, 13);
        var payDate = new DateOnly(2024, 10, 13);

        var result = _calculator.CalculateInterest(
            principal: 76000m,
            monthlyInterestRate: 1.5m,
            monthlyPenalRate: 0.5m,
            pledgeDate: new DateOnly(2024, 6, 15),
            calculateUpToDate: payDate,
            lastInterestPaidDate: lastPaid);

        // 1 month on ₹76,000 = ₹1,140
        result.TotalDays.Should().Be(30);
        result.RegularInterest.Should().Be(1140.00m);
    }

    [Fact]
    public void Scenario4_FullClosure_TwoMonthsOnReducedBalance()
    {
        // Remaining principal ₹56,000. Last interest paid up to 13-Oct-2024. Closing date: 12-Dec-2024 (60 days = 2 months)
        var lastPaid = new DateOnly(2024, 10, 13);
        var closeDate = new DateOnly(2024, 12, 12);

        var result = _calculator.CalculateInterest(
            principal: 56000m,
            monthlyInterestRate: 1.5m,
            monthlyPenalRate: 0.5m,
            pledgeDate: new DateOnly(2024, 6, 15),
            calculateUpToDate: closeDate,
            lastInterestPaidDate: lastPaid);

        // 60 days = 2 months on ₹56,000 @ 1.5% = ₹56,000 * 1.5% * 2 = ₹1,680
        result.TotalDays.Should().Be(60);
        result.RegularInterest.Should().Be(1680.00m);
        result.TotalOutstandingAmount.Should().Be(57680.00m);
    }

    [Fact]
    public void Scenario5_OverduePenalInterest_BeyondGracePeriod()
    {
        // Principal ₹100,000, pledged 01-May-2024, tenure 12 months (maturity 01-May-2025), grace 7 days (grace expires 08-May-2025)
        // Repayment on 15-Jun-2025 (38 days overdue past grace)
        var pledgeDate = new DateOnly(2024, 5, 1);
        var payDate = new DateOnly(2025, 6, 15);

        var result = _calculator.CalculateInterest(
            principal: 100000m,
            monthlyInterestRate: 1.5m,
            monthlyPenalRate: 0.5m,
            pledgeDate: pledgeDate,
            calculateUpToDate: payDate,
            tenureMonths: 12,
            gracePeriodDays: 7,
            roundUpToNextMonth: true);

        // Regular tenure + grace period charged at 1.5%
        result.RegularInterest.Should().BeGreaterThan(0);
        // Overdue days charged with penal surcharge (1.5% + 0.5% = 2.0%/month)
        result.PenalInterest.Should().BeGreaterThan(0);
        result.Breakdown.Should().HaveCount(2);
    }
}
