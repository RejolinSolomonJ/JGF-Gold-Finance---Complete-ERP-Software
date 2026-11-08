using JGF.Application.Accounting;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JGF.Infrastructure.Services;

public class AccountingReportService : DbServiceBase, IAccountingReportService
{
    private readonly INumberSeriesService _numberSeries;
    private readonly IPermissionService _permissionService;

    [ActivatorUtilitiesConstructor]
    public AccountingReportService(
        IDbContextFactory<JgfDbContext> contextFactory,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
        : base(contextFactory)
    {
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    public AccountingReportService(
        JgfDbContext context,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
        : base(context)
    {
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    public async Task<DayBookSummaryDto> GetDayBookAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        // 1. Calculate Opening Cash prior to fromDate
        var headCash = await context.LedgerHeads.AsNoTracking().FirstOrDefaultAsync(l => l.Code == "1001", ct);
        decimal openingCash = headCash?.OpeningBalance ?? 0m;

        if (headCash != null)
        {
            var priorCashLines = await context.VoucherLines
                .AsNoTracking()
                .Include(vl => vl.Voucher)
                .Where(vl => vl.LedgerHeadId == headCash.Id && vl.Voucher.IsPosted && vl.Voucher.VoucherDate < fromDate)
                .ToListAsync(ct);

            openingCash += priorCashLines.Sum(l => l.DebitAmount - l.CreditAmount);
        }

        // 2. Fetch all voucher lines in the date range
        var vouchers = await context.Vouchers
            .AsNoTracking()
            .Include(v => v.Lines).ThenInclude(l => l.LedgerHead)
            .Where(v => v.IsPosted && v.VoucherDate >= fromDate && v.VoucherDate <= toDate)
            .OrderBy(v => v.VoucherDate).ThenBy(v => v.VoucherNumber)
            .ToListAsync(ct);

        var items = new List<DayBookItemDto>();
        decimal totalReceipts = 0m;
        decimal totalPayments = 0m;

        foreach (var v in vouchers)
        {
            foreach (var line in v.Lines)
            {
                if (headCash != null && line.LedgerHeadId == headCash.Id)
                {
                    totalReceipts += line.DebitAmount;
                    totalPayments += line.CreditAmount;
                    continue;
                }

                var receiptAmt = (v.VoucherType == VoucherType.Receipt) ? line.CreditAmount : 0m;
                var paymentAmt = (v.VoucherType == VoucherType.Payment) ? line.DebitAmount : 0m;

                items.Add(new DayBookItemDto(
                    v.Id,
                    v.VoucherNumber,
                    v.VoucherDate,
                    line.LedgerHead?.Code ?? "-",
                    line.LedgerHead?.Name ?? "-",
                    line.LedgerHead?.AccountGroup ?? LedgerHeadType.Asset,
                    receiptAmt,
                    paymentAmt,
                    !string.IsNullOrWhiteSpace(line.Narration) ? line.Narration : (v.Narration ?? ""),
                    v.ReferenceType,
                    v.ReferenceId
                ));
            }
        }

        var closingCash = openingCash + totalReceipts - totalPayments;

        return new DayBookSummaryDto(
            fromDate,
            toDate,
            openingCash,
            totalReceipts,
            totalPayments,
            closingCash,
            items
        );
    }

    public async Task<TrialBalanceReportDto> GetTrialBalanceAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var heads = await context.LedgerHeads.AsNoTracking().OrderBy(l => l.Code).ToListAsync(ct);
        var lines = await context.VoucherLines
            .AsNoTracking()
            .Include(vl => vl.Voucher)
            .Where(vl => vl.Voucher.IsPosted && vl.Voucher.VoucherDate <= asOfDate)
            .ToListAsync(ct);

        var rows = new List<TrialBalanceRowDto>();
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var head in heads)
        {
            var headLines = lines.Where(l => l.LedgerHeadId == head.Id).ToList();
            var totalDr = headLines.Sum(l => l.DebitAmount);
            var totalCr = headLines.Sum(l => l.CreditAmount);

            if (head.NormalBalance == "Debit") totalDr += head.OpeningBalance;
            else totalCr += head.OpeningBalance;

            var net = totalDr - totalCr;
            decimal drBalance = 0m;
            decimal crBalance = 0m;

            if (net > 0) drBalance = net;
            else if (net < 0) crBalance = Math.Abs(net);

            if (drBalance > 0 || crBalance > 0)
            {
                rows.Add(new TrialBalanceRowDto(head.Code, head.Name, head.AccountGroup, drBalance, crBalance));
                totalDebit += drBalance;
                totalCredit += crBalance;
            }
        }

        return new TrialBalanceReportDto(asOfDate, totalDebit, totalCredit, Math.Abs(totalDebit - totalCredit), rows);
    }

    public async Task<ProfitAndLossReportDto> GetProfitAndLossAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var incomeLines = await context.VoucherLines
            .AsNoTracking()
            .Include(vl => vl.LedgerHead)
            .Include(vl => vl.Voucher)
            .Where(vl => vl.Voucher.IsPosted && 
                         vl.Voucher.VoucherDate >= fromDate && 
                         vl.Voucher.VoucherDate <= toDate &&
                         vl.LedgerHead.AccountGroup == LedgerHeadType.Income)
            .ToListAsync(ct);

        var expenseLines = await context.VoucherLines
            .AsNoTracking()
            .Include(vl => vl.LedgerHead)
            .Include(vl => vl.Voucher)
            .Where(vl => vl.Voucher.IsPosted && 
                         vl.Voucher.VoucherDate >= fromDate && 
                         vl.Voucher.VoucherDate <= toDate &&
                         vl.LedgerHead.AccountGroup == LedgerHeadType.Expense)
            .ToListAsync(ct);

        var incomeRows = incomeLines
            .GroupBy(l => new { l.LedgerHead.Code, l.LedgerHead.Name })
            .Select(g => new IncomeExpenseRowDto(
                g.Key.Code, 
                g.Key.Name, 
                g.Sum(x => x.CreditAmount - x.DebitAmount)))
            .OrderBy(r => r.Code)
            .ToList();

        var expenseRows = expenseLines
            .GroupBy(l => new { l.LedgerHead.Code, l.LedgerHead.Name })
            .Select(g => new IncomeExpenseRowDto(
                g.Key.Code, 
                g.Key.Name, 
                g.Sum(x => x.DebitAmount - x.CreditAmount)))
            .OrderBy(r => r.Code)
            .ToList();

        var totalIncome = incomeRows.Sum(r => r.Amount);
        var totalExpense = expenseRows.Sum(r => r.Amount);
        var netProfit = totalIncome - totalExpense;

        return new ProfitAndLossReportDto(fromDate, toDate, incomeRows, totalIncome, expenseRows, totalExpense, netProfit);
    }

    public async Task<BalanceSheetReportDto> GetBalanceSheetAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var heads = await context.LedgerHeads.AsNoTracking().OrderBy(l => l.Code).ToListAsync(ct);
        var lines = await context.VoucherLines
            .AsNoTracking()
            .Include(vl => vl.Voucher)
            .Where(vl => vl.Voucher.IsPosted && vl.Voucher.VoucherDate <= asOfDate)
            .ToListAsync(ct);

        var assets = new List<BalanceSheetItemDto>();
        var liabilities = new List<BalanceSheetItemDto>();
        var capital = new List<BalanceSheetItemDto>();

        foreach (var head in heads)
        {
            var hLines = lines.Where(l => l.LedgerHeadId == head.Id).ToList();
            var totalDr = hLines.Sum(l => l.DebitAmount);
            var totalCr = hLines.Sum(l => l.CreditAmount);

            if (head.AccountGroup == LedgerHeadType.Asset)
            {
                totalDr += head.OpeningBalance;
                var net = totalDr - totalCr;
                if (net != 0) assets.Add(new BalanceSheetItemDto(head.Code, head.Name, net));
            }
            else if (head.AccountGroup == LedgerHeadType.Liability)
            {
                totalCr += head.OpeningBalance;
                var net = totalCr - totalDr;
                if (net != 0) liabilities.Add(new BalanceSheetItemDto(head.Code, head.Name, net));
            }
            else if (head.AccountGroup == LedgerHeadType.Capital)
            {
                totalCr += head.OpeningBalance;
                var net = totalCr - totalDr;
                if (net != 0) capital.Add(new BalanceSheetItemDto(head.Code, head.Name, net));
            }
        }

        // Calculate Net Profit up to asOfDate
        var pnl = await GetProfitAndLossAsync(DateOnly.MinValue, asOfDate, ct);
        var netProfit = pnl.NetProfitOrLoss;

        var totalAssets = assets.Sum(a => a.Amount);
        var totalLiabilities = liabilities.Sum(l => l.Amount);
        var totalCapital = capital.Sum(c => c.Amount);
        var totalLiabAndCapital = totalLiabilities + totalCapital + netProfit;
        var diff = Math.Abs(totalAssets - totalLiabAndCapital);

        return new BalanceSheetReportDto(
            asOfDate,
            assets,
            totalAssets,
            liabilities,
            totalLiabilities,
            capital,
            totalCapital,
            netProfit,
            totalLiabAndCapital,
            diff
        );
    }

    public async Task<AccountingIntegrityCheckDto> RunIntegrityAuditAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        // 1. Audit Vouchers where TotalDebit != TotalCredit
        var vouchers = await context.Vouchers.AsNoTracking().Include(v => v.Lines).Where(v => v.IsPosted).ToListAsync(ct);
        var unbalanced = new List<string>();

        foreach (var v in vouchers)
        {
            var lineDr = v.Lines.Sum(l => l.DebitAmount);
            var lineCr = v.Lines.Sum(l => l.CreditAmount);
            if (lineDr != lineCr || v.TotalDebit != v.TotalCredit || lineDr != v.TotalDebit)
            {
                unbalanced.Add($"Voucher #{v.VoucherNumber} ({v.VoucherDate:dd-MMM-yyyy}): Header Dr={v.TotalDebit}, Cr={v.TotalCredit}; Lines Dr={lineDr}, Cr={lineCr}");
            }
        }

        // 2. Audit Balance Sheet difference
        var bs = await GetBalanceSheetAsync(asOfDate, ct);

        // 3. Audit Cash Book Closing vs Balance Sheet Cash asset
        var dbSummary = await GetDayBookAsync(DateOnly.MinValue, asOfDate, ct);
        var bsCash = bs.Assets.FirstOrDefault(a => a.Code == "1001")?.Amount ?? 0m;
        var cashDiff = Math.Abs(dbSummary.ClosingCash - bsCash);

        // 4. Audit Loan Module Outstanding Principal vs Jewel Loan Ledger (1002)
        var activeLoansPrincipal = await context.Loans
            .AsNoTracking()
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .SumAsync(l => l.LoanAmount, ct);

        var bsJewelLoan = bs.Assets.FirstOrDefault(a => a.Code == "1002")?.Amount ?? 0m;
        var loanDiff = Math.Abs(activeLoansPrincipal - bsJewelLoan);

        // 5. Audit Bank Repledge Module vs Ledger (2001)
        var activeRepledgeAmount = await context.BankPledges
            .AsNoTracking()
            .Where(b => b.Status == BankPledgeStatus.Active || b.Status == BankPledgeStatus.PartiallyReleased)
            .SumAsync(b => b.DisbursedAmount, ct);

        var bsRepledge = bs.Liabilities.FirstOrDefault(l => l.Code == "2001")?.Amount ?? 0m;
        var repledgeDiff = Math.Abs(activeRepledgeAmount - bsRepledge);

        var hasDiscrepancy = unbalanced.Count > 0 || bs.Difference > 0.01m || cashDiff > 0.01m || loanDiff > 0.01m || repledgeDiff > 0.01m;

        return new AccountingIntegrityCheckDto(
            hasDiscrepancy,
            unbalanced.Count,
            unbalanced,
            bs.Difference,
            dbSummary.ClosingCash,
            bsCash,
            cashDiff,
            activeLoansPrincipal,
            bsJewelLoan,
            loanDiff,
            activeRepledgeAmount,
            bsRepledge,
            repledgeDiff
        );
    }

    public async Task<string> PostManualVoucherAsync(CreateManualVoucherDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.ManageVouchers);

        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var targetHead = await context.LedgerHeads.FindAsync(new object[] { dto.LedgerHeadId }, ct)
            ?? throw new ArgumentException("Ledger head not found.");

        var cashHead = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct)
            ?? throw new InvalidOperationException("Cash ledger head 1001 not configured.");

        var isRelational = context.Database.IsRelational();
        using var tx = isRelational ? await context.Database.BeginTransactionAsync(ct) : null;

        var fy = "2024-25";
        var voucherNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);

        var voucher = new Voucher
        {
            VoucherNumber = voucherNumber,
            VoucherDate = dto.VoucherDate,
            VoucherType = dto.VoucherType,
            TotalDebit = dto.Amount,
            TotalCredit = dto.Amount,
            Narration = dto.Narration,
            IsPosted = true,
            PostedAtUtc = DateTime.UtcNow,
            PostedByUserId = dto.UserId,
            ReferenceType = "ManualEntry"
        };

        if (dto.VoucherType == VoucherType.Receipt)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = cashHead.Id,
                DebitAmount = dto.Amount,
                CreditAmount = 0m,
                Narration = dto.Narration,
                LineOrder = 1
            });

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = targetHead.Id,
                DebitAmount = 0m,
                CreditAmount = dto.Amount,
                Narration = dto.Narration,
                LineOrder = 2
            });
        }
        else // Payment
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = targetHead.Id,
                DebitAmount = dto.Amount,
                CreditAmount = 0m,
                Narration = dto.Narration,
                LineOrder = 1
            });

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = cashHead.Id,
                DebitAmount = 0m,
                CreditAmount = dto.Amount,
                Narration = dto.Narration,
                LineOrder = 2
            });
        }

        await context.Vouchers.AddAsync(voucher, ct);
        await context.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return voucher.VoucherNumber;
    }
}
