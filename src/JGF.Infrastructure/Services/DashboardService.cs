using JGF.Application.Dashboard;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JGF.Infrastructure.Services;

public class DashboardService : DbServiceBase, IDashboardService
{
    private readonly IBackupService _backupService;

    [ActivatorUtilitiesConstructor]
    public DashboardService(IDbContextFactory<JgfDbContext> contextFactory, IBackupService backupService)
        : base(contextFactory)
    {
        _backupService = backupService;
    }

    internal DashboardService(JgfDbContext context, IBackupService backupService)
        : base(context)
    {
        _backupService = backupService;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);

        // 1. Current Cash Balance (Head 1001)
        var headCash = await context.LedgerHeads.AsNoTracking().FirstOrDefaultAsync(l => l.Code == "1001", ct);
        decimal currentCash = headCash?.OpeningBalance ?? 0m;
        if (headCash != null)
        {
            var cashLines = await context.VoucherLines
                .AsNoTracking()
                .Where(vl => vl.LedgerHeadId == headCash.Id && vl.Voucher.IsPosted)
                .Select(vl => new { vl.DebitAmount, vl.CreditAmount })
                .ToListAsync(ct);

            currentCash += cashLines.Sum(l => l.DebitAmount - l.CreditAmount);
        }

        // 2. Loans Issued Today & This Month (Single scan on current month)
        var recentMonthLoans = await context.Loans
            .AsNoTracking()
            .Where(l => l.PledgeDate >= firstDayOfMonth && l.PledgeDate <= today)
            .Select(l => new { l.PledgeDate, l.LoanAmount })
            .ToListAsync(ct);

        var todayLoans = recentMonthLoans.Where(l => l.PledgeDate == today).ToList();
        int loansIssuedTodayCount = todayLoans.Count;
        decimal loansIssuedTodayAmount = todayLoans.Sum(l => l.LoanAmount);

        int loansIssuedThisMonthCount = recentMonthLoans.Count;
        decimal loansIssuedThisMonthAmount = recentMonthLoans.Sum(l => l.LoanAmount);

        // 3. Collections Today
        var receiptsTodayQuery = context.Receipts.AsNoTracking().Where(r => r.ReceiptDate == today);
        decimal collectionsTodayTotal = await receiptsTodayQuery.SumAsync(r => (decimal?)r.TotalAmount, ct) ?? 0m;
        decimal collectionsTodayPrincipal = await receiptsTodayQuery.SumAsync(r => (decimal?)r.PrincipalAmount, ct) ?? 0m;
        decimal collectionsTodayInterest = await receiptsTodayQuery.SumAsync(r => (decimal?)(r.InterestAmount + r.PenalInterestAmount), ct) ?? 0m;

        // 4. Active Loans & Outstanding Principal & Custody Split (Single Consolidated Scan)
        var activeLoans = await context.Loans
            .AsNoTracking()
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .Select(l => new { l.LoanAmount, l.NetWeight, l.MaturityDate })
            .ToListAsync(ct);

        int totalActiveLoansCount = activeLoans.Count;
        decimal totalLoanAmount = activeLoans.Sum(l => l.LoanAmount);
        decimal totalNetWeight = activeLoans.Sum(l => l.NetWeight);

        var overdueLoans = activeLoans.Where(l => today > l.MaturityDate).ToList();
        int overdueLoansCount = overdueLoans.Count;
        decimal overdueLoansPrincipal = overdueLoans.Sum(l => l.LoanAmount);

        decimal totalPrincipalPaid = await context.Receipts
            .AsNoTracking()
            .Where(r => r.Loan.Status == LoanStatus.Active || r.Loan.Status == LoanStatus.PartiallyPaid)
            .SumAsync(r => (decimal?)r.PrincipalAmount, ct) ?? 0m;

        decimal totalOutstandingPrincipal = Math.Max(0m, totalLoanAmount - totalPrincipalPaid);

        decimal bankCustodyWeight = await context.BankPledgeLoans
            .AsNoTracking()
            .Where(bpl => !bpl.IsReleased && !bpl.IsDeleted && (bpl.Loan.Status == LoanStatus.Active || bpl.Loan.Status == LoanStatus.PartiallyPaid))
            .SumAsync(bpl => (decimal?)bpl.Loan.NetWeight, ct) ?? 0m;

        decimal shopLockerWeight = Math.Max(0m, totalNetWeight - bankCustodyWeight);

        // 5. Bank Repledge Exposure & Upcoming Dues
        var activeBankPledges = await context.BankPledges
            .AsNoTracking()
            .Include(bp => bp.Bank)
            .Where(bp => bp.Status == BankPledgeStatus.Active || bp.Status == BankPledgeStatus.PartiallyReleased)
            .ToListAsync(ct);

        decimal bankRepledgeTotalExposure = activeBankPledges.Sum(bp => bp.DisbursedAmount);
        int bankRepledgeActiveCount = activeBankPledges.Count;

        var upcomingDues = new List<BankUpcomingDueDto>();
        foreach (var bp in activeBankPledges.Where(b => b.MaturityDate.HasValue))
        {
            var mDate = bp.MaturityDate!.Value;
            var daysRemaining = (mDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
            if (daysRemaining <= 30)
            {
                upcomingDues.Add(new BankUpcomingDueDto(
                    bp.Id,
                    bp.PledgeNumber,
                    bp.Bank?.Name ?? "Bank",
                    mDate,
                    bp.DisbursedAmount,
                    daysRemaining
                ));
            }
        }
        upcomingDues = upcomingDues.OrderBy(u => u.DaysRemaining).ToList();

        // 6. Backup Health
        var lastBackupLog = await context.BackupLogs
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        bool hasRecentSuccessfulBackup = await _backupService.HasRecentSuccessfulBackupAsync(TimeSpan.FromHours(24), ct);
        DateTime? lastBackupTimeUtc = lastBackupLog?.CreatedAtUtc;

        // 7. Monthly Trends (Consolidated 2-Query Aggregation for 6 Months)
        var sixMonthsAgo = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        var monthlyLoanStats = await context.Loans
            .AsNoTracking()
            .Where(l => l.PledgeDate >= sixMonthsAgo)
            .GroupBy(l => new { l.PledgeDate.Year, l.PledgeDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(l => l.LoanAmount) })
            .ToListAsync(ct);

        var monthlyReceiptStats = await context.Receipts
            .AsNoTracking()
            .Where(r => r.ReceiptDate >= sixMonthsAgo)
            .GroupBy(r => new { r.ReceiptDate.Year, r.ReceiptDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Collections = g.Sum(r => r.TotalAmount),
                Interest = g.Sum(r => r.InterestAmount + r.PenalInterestAmount)
            })
            .ToListAsync(ct);

        var monthlyTrends = new List<MonthlyTrendDto>(6);
        for (int i = 5; i >= 0; i--)
        {
            var targetMonthDate = today.AddMonths(-i);
            var y = targetMonthDate.Year;
            var m = targetMonthDate.Month;

            var lStat = monthlyLoanStats.FirstOrDefault(s => s.Year == y && s.Month == m);
            var rStat = monthlyReceiptStats.FirstOrDefault(s => s.Year == y && s.Month == m);

            monthlyTrends.Add(new MonthlyTrendDto(
                targetMonthDate.ToString("MMM yyyy"),
                y,
                m,
                lStat?.Total ?? 0m,
                rStat?.Collections ?? 0m,
                rStat?.Interest ?? 0m
            ));
        }

        return new DashboardSummaryDto(
            currentCash,
            loansIssuedTodayCount,
            loansIssuedTodayAmount,
            loansIssuedThisMonthCount,
            loansIssuedThisMonthAmount,
            collectionsTodayTotal,
            collectionsTodayPrincipal,
            collectionsTodayInterest,
            totalOutstandingPrincipal,
            totalActiveLoansCount,
            shopLockerWeight,
            bankCustodyWeight,
            shopLockerWeight + bankCustodyWeight,
            overdueLoansCount,
            overdueLoansPrincipal,
            bankRepledgeTotalExposure,
            bankRepledgeActiveCount,
            upcomingDues,
            lastBackupTimeUtc,
            hasRecentSuccessfulBackup,
            monthlyTrends
        );
    }
}
