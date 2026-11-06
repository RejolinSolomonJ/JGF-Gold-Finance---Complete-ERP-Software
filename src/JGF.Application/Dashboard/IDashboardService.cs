namespace JGF.Application.Dashboard;

public record BankUpcomingDueDto(
    Guid BankPledgeId,
    string RepledgeNumber,
    string BankName,
    DateOnly DueDate,
    decimal Amount,
    int DaysRemaining
);

public record MonthlyTrendDto(
    string MonthLabel,
    int Year,
    int Month,
    decimal LoansDisbursedAmount,
    decimal CollectionsAmount,
    decimal InterestIncomeAmount
);

public record DashboardSummaryDto(
    decimal TodayCashBalance,
    int LoansIssuedTodayCount,
    decimal LoansIssuedTodayAmount,
    int LoansIssuedThisMonthCount,
    decimal LoansIssuedThisMonthAmount,
    decimal CollectionsTodayTotal,
    decimal CollectionsTodayPrincipal,
    decimal CollectionsTodayInterest,
    decimal TotalOutstandingPrincipal,
    int TotalActiveLoansCount,
    decimal TotalGoldWeightShopLocker,
    decimal TotalGoldWeightBankCustody,
    decimal TotalGoldWeightOverall,
    int OverdueLoansCount,
    decimal OverdueLoansPrincipal,
    decimal BankRepledgeTotalExposure,
    int BankRepledgeActiveCount,
    List<BankUpcomingDueDto> BankUpcomingDues,
    DateTime? LastBackupTimeUtc,
    bool HasRecentSuccessfulBackup,
    List<MonthlyTrendDto> MonthlyTrends
);

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);
}

