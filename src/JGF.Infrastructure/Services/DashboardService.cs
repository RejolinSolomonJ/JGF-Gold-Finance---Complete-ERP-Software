using JGF.Application.Dashboard;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly JgfDbContext _context;
    private readonly IBackupService _backupService;

    public DashboardService(JgfDbContext context, IBackupService backupService)
    {
        _context = context;
        _backupService = backupService;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);

        // 1. Current Cash Balance (Head 1001)
        var headCash = await _context.LedgerHeads.AsNoTracking().FirstOrDefaultAsync(l => l.Code == "1001", ct);
        decimal currentCash = headCash?.OpeningBalance ?? 0m;
        if (headCash != null)
        {
            var cashLines = await _context.VoucherLines
                .AsNoTracking()
                .Where(vl => vl.LedgerHeadId == headCash.Id && vl.Voucher.IsPosted)
                .Select(vl => new { vl.DebitAmount, vl.CreditAmount })
                .ToListAsync(ct);

            currentCash += cashLines.Sum(l => l.DebitAmount - l.CreditAmount);
        }

        // 2. Loans Issued Today & This Month
        var loansToday = await _context.Loans
            .AsNoTracking()
            .Where(l => l.PledgeDate == today)
            .Select(l => l.LoanAmount)
            .ToListAsync(ct);

        var loansMonth = await _context.Loans
            .AsNoTracking()
            .Where(l => l.PledgeDate >= firstDayOfMonth && l.PledgeDate <= today)
            .Select(l => l.LoanAmount)
            .ToListAsync(ct);

        int loansIssuedTodayCount = loansToday.Count;
        decimal loansIssuedTodayAmount = loansToday.Sum();

        int loansIssuedThisMonthCount = loansMonth.Count;
        decimal loansIssuedThisMonthAmount = loansMonth.Sum();

        // 3. Collections Today
        var receiptsToday = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.ReceiptDate == today)
            .Select(r => new { r.TotalAmount, r.PrincipalAmount, r.InterestAmount, r.PenalInterestAmount })
            .ToListAsync(ct);

        decimal collectionsTodayTotal = receiptsToday.Sum(r => r.TotalAmount);
        decimal collectionsTodayPrincipal = receiptsToday.Sum(r => r.PrincipalAmount);
        decimal collectionsTodayInterest = receiptsToday.Sum(r => r.InterestAmount + r.PenalInterestAmount);

        // 4. Active Loans & Outstanding Principal & Custody Split
        var activeLoans = await _context.Loans
            .AsNoTracking()
            .Include(l => l.Receipts)
            .Include(l => l.BankPledgeLoans)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .ToListAsync(ct);

        int totalActiveLoansCount = activeLoans.Count;
        decimal totalOutstandingPrincipal = 0m;
        decimal shopLockerWeight = 0m;
        decimal bankCustodyWeight = 0m;
        int overdueLoansCount = 0;
        decimal overdueLoansPrincipal = 0m;

        foreach (var loan in activeLoans)
        {
            var principalBalance = Math.Max(0m, loan.LoanAmount - loan.Receipts.Sum(r => r.PrincipalAmount));
            totalOutstandingPrincipal += principalBalance;

            var isBankCustody = loan.BankPledgeLoans.Any(bpl => !bpl.IsReleased && !bpl.IsDeleted);
            if (isBankCustody)
            {
                bankCustodyWeight += loan.NetWeight;
            }
            else
            {
                shopLockerWeight += loan.NetWeight;
            }

            if (today > loan.MaturityDate)
            {
                overdueLoansCount++;
                overdueLoansPrincipal += principalBalance;
            }
        }

        // 5. Bank Repledge Exposure & Upcoming Dues
        var activeBankPledges = await _context.BankPledges
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
        var lastBackupLog = await _context.BackupLogs
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        bool hasRecentSuccessfulBackup = await _backupService.HasRecentSuccessfulBackupAsync(TimeSpan.FromHours(24), ct);
        DateTime? lastBackupTimeUtc = lastBackupLog?.CreatedAtUtc;

        // 7. Monthly Trends (Last 6 Months)
        var monthlyTrends = new List<MonthlyTrendDto>();
        for (int i = 5; i >= 0; i--)
        {
            var targetMonthDate = today.AddMonths(-i);
            var mStart = new DateOnly(targetMonthDate.Year, targetMonthDate.Month, 1);
            var mEnd = mStart.AddMonths(1).AddDays(-1);

            var mLoansDisbursed = await _context.Loans
                .AsNoTracking()
                .Where(l => l.PledgeDate >= mStart && l.PledgeDate <= mEnd)
                .SumAsync(l => l.LoanAmount, ct);

            var mReceipts = await _context.Receipts
                .AsNoTracking()
                .Where(r => r.ReceiptDate >= mStart && r.ReceiptDate <= mEnd)
                .Select(r => new { r.TotalAmount, r.InterestAmount, r.PenalInterestAmount })
                .ToListAsync(ct);

            var mCollections = mReceipts.Sum(r => r.TotalAmount);
            var mInterest = mReceipts.Sum(r => r.InterestAmount + r.PenalInterestAmount);

            monthlyTrends.Add(new MonthlyTrendDto(
                mStart.ToString("MMM yyyy"),
                mStart.Year,
                mStart.Month,
                mLoansDisbursed,
                mCollections,
                mInterest
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
