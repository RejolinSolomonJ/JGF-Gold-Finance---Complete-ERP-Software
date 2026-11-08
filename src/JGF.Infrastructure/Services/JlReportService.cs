using JGF.Application.Loans;
using JGF.Application.Reports;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class JlReportService : IJlReportService
{
    private readonly JgfDbContext _context;
    private readonly INumberSeriesService _numberSeries;
    private readonly IPermissionService _permissionService;
    private readonly IInterestCalculator _interestCalculator;

    public JlReportService(
        JgfDbContext context,
        INumberSeriesService numberSeries,
        IPermissionService permissionService,
        IInterestCalculator interestCalculator)
    {
        _context = context;
        _numberSeries = numberSeries;
        _permissionService = permissionService;
        _interestCalculator = interestCalculator;
    }

    public async Task<OutstandingReportSummaryDto> GetOutstandingReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Loans
            .AsNoTracking();

        // Status filter: Default to active/partially paid if null
        if (filter.Status.HasValue)
        {
            query = query.Where(l => l.Status == filter.Status.Value);
        }
        else
        {
            query = query.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid);
        }

        if (filter.FromDate.HasValue)
            query = query.Where(l => l.PledgeDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(l => l.PledgeDate <= filter.ToDate.Value);
        if (filter.SchemeId.HasValue)
            query = query.Where(l => l.SchemeId == filter.SchemeId.Value);
        if (filter.CustomerId.HasValue)
            query = query.Where(l => l.CustomerId == filter.CustomerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim().ToLower();
            query = query.Where(l => l.LoanNumber.ToLower().Contains(st) ||
                                     l.Customer.FullName.ToLower().Contains(st) ||
                                     l.Customer.MobileNumber.Contains(st) ||
                                     (l.PacketNumber != null && l.PacketNumber.ToLower().Contains(st)));
        }

        // Server-side paging / screen limit: no screen may load an unbounded table (Requirement 4)
        var loans = await query
            .OrderBy(l => l.PledgeDate)
            .ThenBy(l => l.LoanNumber)
            .Take(1000)
            .Select(l => new
            {
                l.Id,
                l.LoanNumber,
                l.PledgeDate,
                l.MaturityDate,
                CustomerName = l.Customer.FullName,
                CustomerMobile = l.Customer.MobileNumber,
                SchemeName = l.Scheme.Name,
                l.Scheme.InterestRatePerMonth,
                l.Scheme.PenalInterestRatePerMonth,
                l.Scheme.TenureMonths,
                l.Scheme.GracePeriodDays,
                l.Scheme.MinimumInterestDays,
                l.GrossWeight,
                l.NetWeight,
                l.LoanAmount,
                l.LastInterestPaidDate,
                l.Status,
                l.PacketNumber,
                PrincipalRepaid = l.Receipts.Sum(r => (decimal?)r.PrincipalAmount) ?? 0m,
                BankCustodyName = l.BankPledgeLoans
                    .Where(b => !b.IsReleased && !b.IsDeleted)
                    .Select(b => b.BankPledge.Bank.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = new List<OutstandingLoanItemDto>(loans.Count);
        decimal grandGrossWt = 0m;
        decimal grandNetWt = 0m;
        decimal grandDisbursed = 0m;
        decimal grandOutstanding = 0m;
        decimal grandAccruedInterest = 0m;
        decimal grandPayable = 0m;

        foreach (var l in loans)
        {
            var isBankCustody = l.BankCustodyName != null;
            var storageLoc = isBankCustody 
                ? $"Bank Custody ({l.BankCustodyName ?? "Bank"})" 
                : "Shop Locker";

            if (filter.StorageLocation == "ShopLocker" && isBankCustody) continue;
            if (filter.StorageLocation == "BankCustody" && !isBankCustody) continue;

            var principalBalance = Math.Max(0m, l.LoanAmount - l.PrincipalRepaid);

            var calcFrom = l.LastInterestPaidDate ?? l.PledgeDate;
            var daysElapsed = Math.Max(0, (today.ToDateTime(TimeOnly.MinValue) - calcFrom.ToDateTime(TimeOnly.MinValue)).Days);

            // Compute accrued interest
            var calcResult = _interestCalculator.CalculateInterest(
                principalBalance,
                l.InterestRatePerMonth,
                l.PenalInterestRatePerMonth,
                l.PledgeDate,
                today,
                l.LastInterestPaidDate,
                l.TenureMonths,
                l.GracePeriodDays,
                l.MinimumInterestDays);

            var accruedInterest = calcResult.TotalInterest;
            var isOverdue = today > l.MaturityDate;
            var totalPayable = principalBalance + accruedInterest;

            items.Add(new OutstandingLoanItemDto(
                l.Id,
                l.LoanNumber,
                l.PledgeDate,
                l.MaturityDate,
                l.CustomerName,
                l.CustomerMobile,
                l.SchemeName,
                l.GrossWeight,
                l.NetWeight,
                l.LoanAmount,
                principalBalance,
                daysElapsed,
                accruedInterest,
                totalPayable,
                l.Status,
                isOverdue,
                l.PacketNumber,
                storageLoc
            ));

            grandGrossWt += l.GrossWeight;
            grandNetWt += l.NetWeight;
            grandDisbursed += l.LoanAmount;
            grandOutstanding += principalBalance;
            grandAccruedInterest += accruedInterest;
            grandPayable += totalPayable;
        }

        return new OutstandingReportSummaryDto(
            items.Count,
            grandGrossWt,
            grandNetWt,
            grandDisbursed,
            grandOutstanding,
            grandAccruedInterest,
            grandPayable,
            items
        );
    }

    public async Task<LoanSummaryReportDto> GetLoanSummaryReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.Loans
            .AsNoTracking()
            .Include(l => l.Scheme)
            .Include(l => l.Receipts)
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(l => l.PledgeDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(l => l.PledgeDate <= filter.ToDate.Value);
        if (filter.SchemeId.HasValue)
            query = query.Where(l => l.SchemeId == filter.SchemeId.Value);

        var loans = await query.ToListAsync(ct);
        var schemes = await _context.Schemes.AsNoTracking().OrderBy(s => s.Code).ToListAsync(ct);

        var summaryItems = new List<LoanSummaryItemDto>();

        foreach (var sc in schemes)
        {
            var scLoans = loans.Where(l => l.SchemeId == sc.Id).ToList();
            if (scLoans.Count == 0 && filter.SchemeId.HasValue) continue;

            var active = scLoans.Count(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid);
            var closed = scLoans.Count(l => l.Status == LoanStatus.Closed);
            var disbursed = scLoans.Sum(l => l.LoanAmount);
            var principalCollected = scLoans.SelectMany(l => l.Receipts).Sum(r => r.PrincipalAmount);
            var interestCollected = scLoans.SelectMany(l => l.Receipts).Sum(r => r.InterestAmount + r.PenalInterestAmount);
            var grossWt = scLoans.Sum(l => l.GrossWeight);
            var netWt = scLoans.Sum(l => l.NetWeight);

            // Active outstanding principal
            var activeLoans = scLoans.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid).ToList();
            var outstanding = activeLoans.Sum(l => l.LoanAmount - l.Receipts.Sum(r => r.PrincipalAmount));

            summaryItems.Add(new LoanSummaryItemDto(
                sc.Id,
                sc.Code,
                sc.Name,
                scLoans.Count,
                active,
                closed,
                disbursed,
                principalCollected,
                outstanding,
                grossWt,
                netWt,
                interestCollected
            ));
        }

        return new LoanSummaryReportDto(
            summaryItems.Count,
            summaryItems.Sum(i => i.TotalLoansCount),
            summaryItems.Sum(i => i.ActiveLoansCount),
            summaryItems.Sum(i => i.ClosedLoansCount),
            summaryItems.Sum(i => i.TotalDisbursedAmount),
            summaryItems.Sum(i => i.TotalPrincipalCollected),
            summaryItems.Sum(i => i.TotalOutstandingPrincipal),
            summaryItems.Sum(i => i.TotalGrossWeight),
            summaryItems.Sum(i => i.TotalNetWeight),
            summaryItems.Sum(i => i.TotalInterestCollected),
            summaryItems
        );
    }

    public async Task<PledgeReportSummaryDto> GetPledgeReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .Include(l => l.Items)
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(l => l.PledgeDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(l => l.PledgeDate <= filter.ToDate.Value);
        if (filter.SchemeId.HasValue)
            query = query.Where(l => l.SchemeId == filter.SchemeId.Value);
        if (filter.CustomerId.HasValue)
            query = query.Where(l => l.CustomerId == filter.CustomerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim().ToLower();
            query = query.Where(l => l.LoanNumber.ToLower().Contains(st) ||
                                     l.Customer.FullName.ToLower().Contains(st) ||
                                     l.Customer.MobileNumber.Contains(st));
        }

        var loans = await query.OrderByDescending(l => l.PledgeDate).ThenByDescending(l => l.LoanNumber).ToListAsync(ct);

        var items = loans.Select(l => new PledgeReportItemDto(
            l.Id,
            l.LoanNumber,
            l.PledgeDate,
            l.MaturityDate,
            l.Customer.FullName,
            l.Customer.MobileNumber,
            l.Scheme.Name,
            l.Items.Sum(i => i.Quantity),
            l.GrossWeight,
            l.NetWeight,
            l.LoanAmount,
            l.Scheme.InterestRate,
            l.PacketNumber
        )).ToList();

        return new PledgeReportSummaryDto(
            items.Count,
            items.Sum(i => i.ItemCount),
            items.Sum(i => i.GrossWeight),
            items.Sum(i => i.NetWeight),
            items.Sum(i => i.LoanAmount),
            items
        );
    }

    public async Task<ReleaseReportSummaryDto> GetReleaseReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.Receipts
            .AsNoTracking()
            .Include(r => r.Loan).ThenInclude(l => l.Customer)
            .Include(r => r.Loan).ThenInclude(l => l.Scheme)
            .Where(r => r.ReceiptType == ReceiptType.Closure)
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.ReceiptDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(r => r.ReceiptDate <= filter.ToDate.Value);
        if (filter.SchemeId.HasValue)
            query = query.Where(r => r.Loan.SchemeId == filter.SchemeId.Value);
        if (filter.CustomerId.HasValue)
            query = query.Where(r => r.Loan.CustomerId == filter.CustomerId.Value);
        if (filter.PaymentMode.HasValue)
            query = query.Where(r => r.PaymentMode == filter.PaymentMode.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim().ToLower();
            query = query.Where(r => r.ReceiptNumber.ToLower().Contains(st) ||
                                     r.Loan.LoanNumber.ToLower().Contains(st) ||
                                     r.Loan.Customer.FullName.ToLower().Contains(st));
        }

        var receipts = await query.OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.ReceiptNumber).ToListAsync(ct);

        var items = receipts.Select(r => new ReleaseReportItemDto(
            r.Id,
            r.ReceiptNumber,
            r.ReceiptDate,
            r.Loan.LoanNumber,
            r.Loan.PledgeDate,
            r.Loan.Customer.FullName,
            r.Loan.Customer.MobileNumber,
            r.Loan.Scheme.Name,
            r.PrincipalAmount,
            r.InterestAmount,
            r.PenalInterestAmount,
            r.OtherCharges + r.PostalCharges,
            r.TotalAmount,
            r.PaymentMode
        )).ToList();

        return new ReleaseReportSummaryDto(
            items.Count,
            items.Sum(i => i.PrincipalPaid),
            items.Sum(i => i.InterestPaid),
            items.Sum(i => i.PenalPaid),
            items.Sum(i => i.OtherAndPostalCharges),
            items.Sum(i => i.TotalAmountPaid),
            items
        );
    }

    public async Task<JlCashReportSummaryDto> GetJlCashReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var loans = await _context.Loans
            .AsNoTracking()
            .Where(l => l.PledgeDate >= fromDate && l.PledgeDate <= toDate)
            .ToListAsync(ct);

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.ReceiptDate >= fromDate && r.ReceiptDate <= toDate)
            .ToListAsync(ct);

        var dates = loans.Select(l => l.PledgeDate)
            .Union(receipts.Select(r => r.ReceiptDate))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var items = new List<JlCashReportItemDto>();

        foreach (var dt in dates)
        {
            var dayLoans = loans.Where(l => l.PledgeDate == dt).ToList();
            var dayReceipts = receipts.Where(r => r.ReceiptDate == dt).ToList();

            var disbursedCount = dayLoans.Count;
            var disbursedAmount = dayLoans.Sum(l => l.LoanAmount);

            var receiptsCount = dayReceipts.Count;
            var principalCollected = dayReceipts.Sum(r => r.PrincipalAmount);
            var interestCollected = dayReceipts.Sum(r => r.InterestAmount);
            var penalCollected = dayReceipts.Sum(r => r.PenalInterestAmount);
            var postalCollected = dayReceipts.Sum(r => r.PostalCharges);
            var otherCollected = dayReceipts.Sum(r => r.OtherCharges);

            var totalInflow = principalCollected + interestCollected + penalCollected + postalCollected + otherCollected;
            var netMovement = totalInflow - disbursedAmount;

            items.Add(new JlCashReportItemDto(
                dt,
                disbursedCount,
                disbursedAmount,
                receiptsCount,
                principalCollected,
                interestCollected,
                penalCollected,
                postalCollected,
                otherCollected,
                totalInflow,
                netMovement
            ));
        }

        return new JlCashReportSummaryDto(
            fromDate,
            toDate,
            items.Sum(i => i.LoansDisbursedCount),
            items.Sum(i => i.LoansDisbursedAmount),
            items.Sum(i => i.ReceiptsCount),
            items.Sum(i => i.PrincipalCollectedAmount),
            items.Sum(i => i.InterestCollectedAmount),
            items.Sum(i => i.PenalCollectedAmount),
            items.Sum(i => i.PostalChargesCollectedAmount),
            items.Sum(i => i.TotalInflow),
            items.Sum(i => i.NetCashMovement),
            items
        );
    }

    public async Task<AllStockReportSummaryDto> GetAllStockReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var activeLoansQuery = _context.Loans
            .AsNoTracking()
            .Include(l => l.Items).ThenInclude(i => i.JewelType)
            .Include(l => l.BankPledgeLoans)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid);

        if (filter.SchemeId.HasValue)
            activeLoansQuery = activeLoansQuery.Where(l => l.SchemeId == filter.SchemeId.Value);

        var activeLoans = await activeLoansQuery.ToListAsync(ct);
        var jewelTypes = await _context.JewelTypes.AsNoTracking().OrderBy(j => j.Code).ToListAsync(ct);

        var items = new List<AllStockReportItemDto>();

        foreach (var jt in jewelTypes)
        {
            int totalPieces = 0;
            decimal totalGross = 0m;
            decimal totalDeduction = 0m;
            decimal totalNet = 0m;
            decimal shopGross = 0m;
            decimal shopNet = 0m;
            decimal bankGross = 0m;
            decimal bankNet = 0m;

            foreach (var loan in activeLoans)
            {
                var matchingItems = loan.Items.Where(i => i.JewelTypeId == jt.Id).ToList();
                if (matchingItems.Count == 0) continue;

                var isBank = loan.BankPledgeLoans.Any(bpl => !bpl.IsReleased && !bpl.IsDeleted);

                var pieces = matchingItems.Sum(i => i.Quantity);
                var gross = matchingItems.Sum(i => i.GrossWeight);
                var ded = matchingItems.Sum(i => i.StoneWeight);
                var net = matchingItems.Sum(i => i.NetWeight);

                totalPieces += pieces;
                totalGross += gross;
                totalDeduction += ded;
                totalNet += net;

                if (isBank)
                {
                    bankGross += gross;
                    bankNet += net;
                }
                else
                {
                    shopGross += gross;
                    shopNet += net;
                }
            }

            if (totalPieces > 0 || !filter.SchemeId.HasValue)
            {
                items.Add(new AllStockReportItemDto(
                    jt.Id,
                    jt.Code,
                    jt.Name,
                    totalPieces,
                    totalGross,
                    totalDeduction,
                    totalNet,
                    shopGross,
                    shopNet,
                    bankGross,
                    bankNet
                ));
            }
        }

        return new AllStockReportSummaryDto(
            items.Count,
            items.Sum(i => i.TotalPieces),
            items.Sum(i => i.TotalGrossWeight),
            items.Sum(i => i.TotalDeductionWeight),
            items.Sum(i => i.TotalNetWeight),
            items.Sum(i => i.ShopLockerGrossWeight),
            items.Sum(i => i.ShopLockerNetWeight),
            items.Sum(i => i.BankCustodyGrossWeight),
            items.Sum(i => i.BankCustodyNetWeight),
            items
        );
    }

    public async Task<JlStockReportSummaryDto> GetJlStockReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.LoanItems
            .AsNoTracking()
            .Include(i => i.JewelType)
            .Include(i => i.Loan).ThenInclude(l => l.Customer)
            .Include(i => i.Loan).ThenInclude(l => l.BankPledgeLoans).ThenInclude(bpl => bpl.BankPledge).ThenInclude(bp => bp.Bank)
            .AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(i => i.Loan.Status == filter.Status.Value);
        }
        else
        {
            query = query.Where(i => i.Loan.Status == LoanStatus.Active || i.Loan.Status == LoanStatus.PartiallyPaid);
        }

        if (filter.SchemeId.HasValue)
            query = query.Where(i => i.Loan.SchemeId == filter.SchemeId.Value);
        if (filter.CustomerId.HasValue)
            query = query.Where(i => i.Loan.CustomerId == filter.CustomerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim().ToLower();
            query = query.Where(i => i.Loan.LoanNumber.ToLower().Contains(st) ||
                                     i.Loan.Customer.FullName.ToLower().Contains(st) ||
                                     (i.Loan.PacketNumber != null && i.Loan.PacketNumber.ToLower().Contains(st)) ||
                                     i.Description.ToLower().Contains(st));
        }

        var itemsRaw = await query.OrderBy(i => i.Loan.LoanNumber).ThenBy(i => i.ItemNumber).ToListAsync(ct);

        var list = new List<JlStockItemDto>();
        foreach (var item in itemsRaw)
        {
            var activeBankPledge = item.Loan.BankPledgeLoans.FirstOrDefault(b => !b.IsReleased && !b.IsDeleted);
            var isBank = activeBankPledge != null;
            var loc = isBank 
                ? $"Bank Custody ({activeBankPledge!.BankPledge?.Bank?.Name ?? "Bank"})" 
                : "Shop Locker";

            if (filter.StorageLocation == "ShopLocker" && isBank) continue;
            if (filter.StorageLocation == "BankCustody" && !isBank) continue;

            list.Add(new JlStockItemDto(
                item.Id,
                item.Loan.LoanNumber,
                item.Loan.PledgeDate,
                item.Loan.PacketNumber ?? "-",
                item.Loan.Customer.FullName,
                item.Description,
                item.DescriptionTamil,
                item.JewelType?.Name ?? "-",
                item.Quantity,
                item.GrossWeight,
                item.StoneWeight,
                item.NetWeight,
                item.PurityPercentage,
                item.AppraisedValue,
                loc,
                item.Loan.Status
            ));
        }

        return new JlStockReportSummaryDto(
            list.Count,
            list.Sum(i => i.Quantity),
            list.Sum(i => i.GrossWeight),
            list.Sum(i => i.StoneWeight),
            list.Sum(i => i.NetWeight),
            list.Sum(i => i.AppraisedValue),
            list
        );
    }

    public async Task<string> RecordPostalChargeAsync(RecordPostalChargeDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.ManageVouchers);

        var loan = await _context.Loans
            .Include(l => l.Customer)
            .FirstOrDefaultAsync(l => l.Id == dto.LoanId, ct)
            ?? throw new ArgumentException("Loan not found.");

        var cashHead = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct)
            ?? throw new InvalidOperationException("Cash account 1001 not found.");
        var postalIncomeHead = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "4004", ct)
            ?? throw new InvalidOperationException("Postal Charges Recovered account 4004 not found.");

        using var tx = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync(ct) : null;

        var fy = "2024-25";
        var voucherNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);

        // Balanced double-entry voucher: Debit Cash (1001), Credit Postal Charges Recovered (4004)
        var voucher = new Voucher
        {
            VoucherNumber = voucherNumber,
            VoucherDate = dto.ChargeDate,
            VoucherType = VoucherType.Receipt,
            TotalDebit = dto.Amount,
            TotalCredit = dto.Amount,
            Narration = $"Postal charge recovery for Loan {loan.LoanNumber}: {dto.Narration}",
            IsPosted = true,
            PostedAtUtc = DateTime.UtcNow,
            PostedByUserId = dto.UserId,
            ReferenceType = "PostalCharge",
            ReferenceId = loan.Id
        };

        voucher.Lines.Add(new VoucherLine
        {
            LedgerHeadId = cashHead.Id,
            DebitAmount = dto.Amount,
            CreditAmount = 0m,
            Narration = $"Postal charge cash collection for {loan.LoanNumber}",
            LineOrder = 1
        });

        voucher.Lines.Add(new VoucherLine
        {
            LedgerHeadId = postalIncomeHead.Id,
            DebitAmount = 0m,
            CreditAmount = dto.Amount,
            Narration = $"Postal recovery {loan.LoanNumber}",
            LineOrder = 2
        });

        await _context.Vouchers.AddAsync(voucher, ct);

        // Record loan ledger charge
        var lastBalance = await _context.LoanLedgerEntries
            .Where(e => e.LoanId == loan.Id)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.CreatedAtUtc)
            .Select(e => e.Balance)
            .FirstOrDefaultAsync(ct);

        await _context.LoanLedgerEntries.AddAsync(new LoanLedgerEntry
        {
            LoanId = loan.Id,
            EntryDate = dto.ChargeDate,
            EntryType = LoanLedgerEntryType.Charge,
            DebitAmount = dto.Amount,
            CreditAmount = dto.Amount,
            Balance = lastBalance,
            ReferenceType = "PostalVoucher",
            ReferenceId = voucher.Id,
            Narration = $"Postal charge recovered: {dto.Narration}"
        }, ct);

        await _context.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return voucherNumber;
    }

    public async Task<PostalChargeReportSummaryDto> GetPostalChargeReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var postalHead = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "4004", ct);
        if (postalHead == null)
        {
            return new PostalChargeReportSummaryDto(0, 0m, new List<PostalChargeItemDto>());
        }

        // Query vouchers that credited account 4004
        var query = _context.Vouchers
            .AsNoTracking()
            .Include(v => v.Lines)
            .Where(v => v.IsPosted && v.Lines.Any(l => l.LedgerHeadId == postalHead.Id && l.CreditAmount > 0))
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(v => v.VoucherDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(v => v.VoucherDate <= filter.ToDate.Value);

        var vouchers = await query.OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.VoucherNumber).ToListAsync(ct);

        var loanIds = vouchers.Where(v => v.ReferenceId.HasValue).Select(v => v.ReferenceId!.Value).Distinct().ToList();
        var loans = await _context.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Where(l => loanIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);

        var items = new List<PostalChargeItemDto>();
        foreach (var v in vouchers)
        {
            var line = v.Lines.First(l => l.LedgerHeadId == postalHead.Id);
            Loan? loan = null;
            if (v.ReferenceId.HasValue && loans.TryGetValue(v.ReferenceId.Value, out var foundLoan))
            {
                loan = foundLoan;
            }

            if (filter.CustomerId.HasValue && loan?.CustomerId != filter.CustomerId.Value)
                continue;

            items.Add(new PostalChargeItemDto(
                v.Id,
                v.VoucherNumber,
                v.VoucherDate,
                loan?.LoanNumber ?? "-",
                loan?.Customer.FullName ?? "-",
                loan?.Customer.MobileNumber ?? "-",
                line.CreditAmount,
                v.Narration ?? "",
                "Cash"
            ));
        }

        return new PostalChargeReportSummaryDto(
            items.Count,
            items.Sum(i => i.Amount),
            items
        );
    }

    public async Task<InterestCollectionReportSummaryDto> GetInterestCollectionReportAsync(JlReportFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.Receipts
            .AsNoTracking()
            .Include(r => r.Loan).ThenInclude(l => l.Customer)
            .Include(r => r.Loan).ThenInclude(l => l.Scheme)
            .Where(r => r.InterestAmount > 0 || r.PenalInterestAmount > 0)
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.ReceiptDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(r => r.ReceiptDate <= filter.ToDate.Value);
        if (filter.SchemeId.HasValue)
            query = query.Where(r => r.Loan.SchemeId == filter.SchemeId.Value);
        if (filter.CustomerId.HasValue)
            query = query.Where(r => r.Loan.CustomerId == filter.CustomerId.Value);
        if (filter.PaymentMode.HasValue)
            query = query.Where(r => r.PaymentMode == filter.PaymentMode.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim().ToLower();
            query = query.Where(r => r.ReceiptNumber.ToLower().Contains(st) ||
                                     r.Loan.LoanNumber.ToLower().Contains(st) ||
                                     r.Loan.Customer.FullName.ToLower().Contains(st));
        }

        var receipts = await query.OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.ReceiptNumber).ToListAsync(ct);

        var items = receipts.Select(r =>
        {
            var days = r.DaysCharged;
            var months = days / 30;
            return new InterestCollectionItemDto(
                r.Id,
                r.ReceiptNumber,
                r.ReceiptDate,
                r.Loan.LoanNumber,
                r.Loan.Customer.FullName,
                r.Loan.Scheme.Name,
                r.InterestCalculatedFromDate,
                r.InterestCalculatedToDate,
                months,
                days,
                r.InterestAmount,
                r.PenalInterestAmount,
                r.InterestAmount + r.PenalInterestAmount,
                r.PaymentMode
            );
        }).ToList();

        return new InterestCollectionReportSummaryDto(
            items.Count,
            items.Sum(i => i.InterestAmount),
            items.Sum(i => i.PenalInterestAmount),
            items.Sum(i => i.TotalInterestCollected),
            items
        );
    }

    public async Task<CustomerStatementReportDto> GetCustomerStatementAsync(Guid customerId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new ArgumentException("Customer not found.");

        var loans = await _context.Loans
            .AsNoTracking()
            .Include(l => l.Scheme)
            .Include(l => l.Receipts)
            .Include(l => l.LedgerEntries)
            .Where(l => l.CustomerId == customerId)
            .OrderBy(l => l.PledgeDate)
            .ToListAsync(ct);

        var activeLoans = loans.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid).ToList();
        var closedLoans = loans.Where(l => l.Status == LoanStatus.Closed).ToList();

        decimal currentOutstandingPrincipal = 0m;
        decimal currentAccruedInterest = 0m;

        foreach (var al in activeLoans)
        {
            var bal = al.LoanAmount - al.Receipts.Sum(r => r.PrincipalAmount);
            currentOutstandingPrincipal += bal;

            var calcFrom = al.LastInterestPaidDate ?? al.PledgeDate;
            var res = _interestCalculator.CalculateInterest(
                bal,
                al.Scheme.InterestRatePerMonth,
                al.Scheme.PenalInterestRatePerMonth,
                al.PledgeDate,
                today,
                al.LastInterestPaidDate,
                al.Scheme.TenureMonths,
                al.Scheme.GracePeriodDays,
                al.Scheme.MinimumInterestDays);

            currentAccruedInterest += res.TotalInterest;
        }

        var totalPayableNow = currentOutstandingPrincipal + currentAccruedInterest;

        // Build Chronological Ledger Timeline
        var transactions = new List<CustomerStatementTransactionDto>();
        decimal runningPrincipal = 0m;

        // Gather all events:
        // 1. Loan disbursements
        // 2. Receipts
        // 3. Postal charges
        var events = new List<(DateOnly Date, DateTime CreatedAt, string Type, string Ref, string Particulars, decimal Debit, decimal Credit, bool IsPrincipalDisbursement, decimal PrincipalRepaid)>();

        foreach (var l in loans)
        {
            events.Add((
                l.PledgeDate,
                l.CreatedAtUtc,
                "Loan Disbursement",
                l.LoanNumber,
                $"Pledged jewels under scheme {l.Scheme.Name} (Gross: {l.GrossWeight:F3}g, Net: {l.NetWeight:F3}g)",
                l.LoanAmount,
                0m,
                true,
                0m
            ));

            foreach (var r in l.Receipts)
            {
                var pText = r.ReceiptType == ReceiptType.Closure 
                    ? $"Full loan closure for {l.LoanNumber} (Interest: ₹{r.InterestAmount:N2}, Penal: ₹{r.PenalInterestAmount:N2})"
                    : $"Payment received for {l.LoanNumber} (Principal: ₹{r.PrincipalAmount:N2}, Interest: ₹{r.InterestAmount:N2})";

                events.Add((
                    r.ReceiptDate,
                    r.CreatedAtUtc,
                    r.ReceiptType == ReceiptType.Closure ? "Loan Closure" : "Receipt Payment",
                    r.ReceiptNumber,
                    pText,
                    0m,
                    r.TotalAmount,
                    false,
                    r.PrincipalAmount
                ));
            }
        }

        // Sort chronologically
        var sortedEvents = events.OrderBy(e => e.Date).ThenBy(e => e.CreatedAt).ToList();

        foreach (var ev in sortedEvents)
        {
            if (ev.IsPrincipalDisbursement)
            {
                runningPrincipal += ev.Debit;
            }
            else
            {
                runningPrincipal = Math.Max(0m, runningPrincipal - ev.PrincipalRepaid);
            }

            transactions.Add(new CustomerStatementTransactionDto(
                ev.Date,
                ev.Type,
                ev.Ref,
                ev.Particulars,
                ev.Debit,
                ev.Credit,
                runningPrincipal
            ));
        }

        return new CustomerStatementReportDto(
            customer.Id,
            customer.Code,
            customer.FullName,
            customer.FatherOrHusbandName,
            customer.MobileNumber,
            customer.AlternateMobile,
            customer.Address,
            customer.District,
            loans.Count,
            activeLoans.Count,
            closedLoans.Count,
            currentOutstandingPrincipal,
            currentAccruedInterest,
            totalPayableNow,
            transactions
        );
    }
}
