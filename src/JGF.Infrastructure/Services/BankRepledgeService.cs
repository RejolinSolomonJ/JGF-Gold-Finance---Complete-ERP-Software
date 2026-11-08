using JGF.Application.Repledge;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JGF.Infrastructure.Services;

public class BankRepledgeService : DbServiceBase, IBankRepledgeService
{
    private readonly INumberSeriesService _numberSeries;
    private readonly IPermissionService _permissionService;

    [ActivatorUtilitiesConstructor]
    public BankRepledgeService(
        IDbContextFactory<JgfDbContext> contextFactory,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
        : base(contextFactory)
    {
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    internal BankRepledgeService(
        JgfDbContext context,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
        : base(context)
    {
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    public async Task<List<BankPledgeSummaryDto>> GetAllBankPledgesAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var thresholdDate = today.AddDays(15);

        var pledges = await context.BankPledges
            .AsNoTracking()
            .Include(b => b.Bank)
            .Include(b => b.BankPledgeLoans).ThenInclude(bpl => bpl.Loan)
            .OrderByDescending(b => b.PledgeDate)
            .ToListAsync(ct);

        return pledges.Select(p =>
        {
            var isOverdue = p.MaturityDate.HasValue && p.MaturityDate.Value < today && p.Status == BankPledgeStatus.Active;
            var isDueSoon = p.MaturityDate.HasValue && p.MaturityDate.Value >= today && p.MaturityDate.Value <= thresholdDate && p.Status == BankPledgeStatus.Active;

            return new BankPledgeSummaryDto(
                p.Id,
                p.Bank?.Code ?? "-",
                p.Bank?.Name ?? "-",
                p.PledgeNumber,
                p.BankAccountOrLoanNumber,
                p.PledgeDate,
                p.MaturityDate,
                p.BankPledgeLoans.Sum(l => l.PledgedAmount),
                p.DisbursedAmount,
                p.InterestRate,
                p.BankPledgeLoans.Count,
                p.BankPledgeLoans.Count(l => !l.IsReleased),
                p.Status,
                isOverdue,
                isDueSoon
            );
        }).ToList();
    }

    public async Task<BankPledge?> GetBankPledgeDetailsAsync(Guid bankPledgeId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.BankPledges
            .AsNoTracking()
            .Include(b => b.Bank)
            .Include(b => b.BankPledgeLoans).ThenInclude(l => l.Loan).ThenInclude(cl => cl.Customer)
            .Include(b => b.BankPledgeLoans).ThenInclude(l => l.Loan).ThenInclude(cl => cl.Items).ThenInclude(i => i.JewelType)
            .FirstOrDefaultAsync(b => b.Id == bankPledgeId, ct);
    }

    public async Task<List<Loan>> GetAvailableLoansForRepledgeAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var alreadyPledgedLoanIds = await context.BankPledgeLoans
            .AsNoTracking()
            .Where(bpl => !bpl.IsReleased)
            .Select(bpl => bpl.LoanId)
            .ToListAsync(ct);

        return await context.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.Items).ThenInclude(i => i.JewelType)
            .Where(l => (l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid) 
                        && !alreadyPledgedLoanIds.Contains(l.Id))
            .OrderByDescending(l => l.PledgeDate)
            .ToListAsync(ct);
    }

    public async Task<BankPledge> CreateBankPledgeAsync(CreateBankPledgeDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.ManageBankRepledge);

        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var bank = await context.Banks.FindAsync(new object[] { dto.BankId }, ct)
            ?? throw new ArgumentException("Selected Bank/NBFC not found.");

        var isRelational = context.Database.IsRelational();
        using var tx = isRelational ? await context.Database.BeginTransactionAsync(ct) : null;

        var selectedLoans = await context.Loans
            .Where(l => dto.LoanIdsToPledge.Contains(l.Id))
            .ToListAsync(ct);

        decimal totalCustomerAmount = selectedLoans.Sum(l => l.LoanAmount);

        var bankPledge = new BankPledge
        {
            BankId = dto.BankId,
            PledgeNumber = dto.PledgeNumber.Trim(),
            PledgeDate = dto.PledgeDate,
            MaturityDate = dto.MaturityDate,
            TotalPledgeAmount = totalCustomerAmount,
            DisbursedAmount = dto.DisbursedAmount,
            InterestRate = dto.InterestRate,
            BankWeight = dto.BankWeight,
            BankAccountOrLoanNumber = dto.BankAccountOrLoanNumber,
            AccountHolderName = dto.AccountHolderName,
            BankReceiptNumber = dto.BankReceiptNumber,
            Status = BankPledgeStatus.Active,
            Remarks = dto.Remarks
        };

        await context.BankPledges.AddAsync(bankPledge, ct);

        foreach (var loan in selectedLoans)
        {
            var bpl = new BankPledgeLoan
            {
                BankPledgeId = bankPledge.Id,
                LoanId = loan.Id,
                PledgedAmount = loan.LoanAmount,
                PledgedDate = dto.PledgeDate,
                IsReleased = false
            };
            await context.BankPledgeLoans.AddAsync(bpl, ct);
        }

        // Double-entry Voucher
        var fy = "2024-25";
        var headCash = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct);
        var headBankLoan = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "2001", ct);

        if (headCash != null && headBankLoan != null)
        {
            var voucherNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);
            var voucher = new Voucher
            {
                VoucherNumber = voucherNumber,
                VoucherDate = dto.PledgeDate,
                VoucherType = VoucherType.Receipt,
                TotalDebit = dto.DisbursedAmount,
                TotalCredit = dto.DisbursedAmount,
                Narration = $"Repledge ref. #{bankPledge.PledgeNumber} from {bank.Name} ({selectedLoans.Count} customer loans pledged)",
                IsPosted = true,
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = dto.UserId,
                ReferenceType = "BankPledge",
                ReferenceId = bankPledge.Id
            };

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headCash.Id,
                DebitAmount = dto.DisbursedAmount,
                CreditAmount = 0m,
                Narration = $"Cash received on bank repledge {bankPledge.PledgeNumber}",
                LineOrder = 1
            });

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headBankLoan.Id,
                DebitAmount = 0m,
                CreditAmount = dto.DisbursedAmount,
                Narration = $"Repledge loan liability to {bank.Name}",
                LineOrder = 2
            });

            await context.Vouchers.AddAsync(voucher, ct);
        }

        await context.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return bankPledge;
    }

    public async Task<(BankPayment Payment, string RzReceiptNumber)> ProcessBankPaymentAsync(SettleBankPaymentDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.ManageBankRepledge);

        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var bankPledge = await context.BankPledges
            .Include(b => b.Bank)
            .Include(b => b.BankPledgeLoans)
            .FirstOrDefaultAsync(b => b.Id == dto.BankPledgeId, ct)
            ?? throw new ArgumentException("Bank pledge not found.");

        var isRelational = context.Database.IsRelational();
        using var tx = isRelational ? await context.Database.BeginTransactionAsync(ct) : null;

        var fy = "24-25";
        var rzSeries = await context.NumberSerieses
            .FirstOrDefaultAsync(s => s.Prefix == "RZ" && s.FinancialYear == fy, ct);

        if (rzSeries == null)
        {
            rzSeries = new NumberSeries
            {
                SeriesType = NumberSeriesType.ReceiptNumber,
                Prefix = "RZ",
                FinancialYear = fy,
                LastNumber = 0,
                PadLength = 5,
                Separator = "/"
            };
            await context.NumberSerieses.AddAsync(rzSeries, ct);
        }

        rzSeries.LastNumber += 1;
        var rzNumber = $"RZ/{fy}/{rzSeries.LastNumber:D5}";

        var totalAmount = dto.PrincipalPaid + dto.InterestPaid + dto.PostalCharges;

        var payment = new BankPayment
        {
            BankPledgeId = bankPledge.Id,
            RzReceiptNumber = rzNumber,
            PaymentDate = dto.PaymentDate,
            PaymentType = dto.PaymentType,
            PrincipalAmount = dto.PrincipalPaid,
            InterestAmount = dto.InterestPaid,
            PostalCharges = dto.PostalCharges,
            TotalAmount = totalAmount,
            PaymentMode = PaymentMode.Cash,
            ReferenceNumber = dto.ReferenceNumber,
            Notes = dto.Remarks
        };
        await context.Set<BankPayment>().AddAsync(payment, ct);

        if (dto.PrincipalPaid > 0)
        {
            bankPledge.DisbursedAmount = Math.Max(0m, bankPledge.DisbursedAmount - dto.PrincipalPaid);
        }

        if (dto.ReleasedLoanIds != null && dto.ReleasedLoanIds.Count > 0)
        {
            var loansToRelease = bankPledge.BankPledgeLoans
                .Where(l => dto.ReleasedLoanIds.Contains(l.LoanId))
                .ToList();

            foreach (var l in loansToRelease)
            {
                l.IsReleased = true;
                l.ReleasedDate = dto.PaymentDate;
            }
        }

        if (bankPledge.DisbursedAmount == 0 || bankPledge.BankPledgeLoans.All(l => l.IsReleased))
        {
            bankPledge.Status = BankPledgeStatus.FullyReleased;
            foreach (var l in bankPledge.BankPledgeLoans)
            {
                l.IsReleased = true;
                l.ReleasedDate ??= dto.PaymentDate;
            }
        }
        else if (dto.PrincipalPaid > 0 || (dto.ReleasedLoanIds != null && dto.ReleasedLoanIds.Count > 0))
        {
            bankPledge.Status = BankPledgeStatus.PartiallyReleased;
        }

        // Post balanced voucher
        var headCash = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct);
        var headBankLoan = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "2001", ct);
        var headInterestExp = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "5001", ct);
        var headPostalExp = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "5004", ct);

        var vNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);
        var voucher = new Voucher
        {
            VoucherNumber = vNumber,
            VoucherDate = dto.PaymentDate,
            VoucherType = VoucherType.Payment,
            TotalDebit = totalAmount,
            TotalCredit = totalAmount,
            Narration = $"Payment voucher #{rzNumber} to {bankPledge.Bank.Name} on repledge #{bankPledge.PledgeNumber}",
            IsPosted = true,
            PostedAtUtc = DateTime.UtcNow,
            PostedByUserId = dto.UserId,
            ReferenceType = "BankPayment",
            ReferenceId = payment.Id
        };

        int line = 1;
        if (dto.PrincipalPaid > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headBankLoan!.Id,
                DebitAmount = dto.PrincipalPaid,
                CreditAmount = 0m,
                Narration = $"Principal repaid on bank pledge {bankPledge.PledgeNumber}",
                LineOrder = line++
            });
        }

        if (dto.InterestPaid > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headInterestExp!.Id,
                DebitAmount = dto.InterestPaid,
                CreditAmount = 0m,
                Narration = $"Interest paid on bank pledge {bankPledge.PledgeNumber}",
                LineOrder = line++
            });
        }

        if (dto.PostalCharges > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headPostalExp!.Id,
                DebitAmount = dto.PostalCharges,
                CreditAmount = 0m,
                Narration = $"Bank postal/notice charges on pledge {bankPledge.PledgeNumber}",
                LineOrder = line++
            });
        }

        voucher.Lines.Add(new VoucherLine
        {
            LedgerHeadId = headCash!.Id,
            DebitAmount = 0m,
            CreditAmount = totalAmount,
            Narration = $"Cash payment to {bankPledge.Bank.Name} under #{rzNumber}",
            LineOrder = line++
        });

        await context.Vouchers.AddAsync(voucher, ct);
        payment.VoucherId = voucher.Id;

        await context.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return (payment, rzNumber);
    }

    public async Task<bool> IsLoanInBankCustodyAsync(Guid loanId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.BankPledgeLoans
            .AsNoTracking()
            .AnyAsync(bpl => bpl.LoanId == loanId && !bpl.IsReleased, ct);
    }

    public async Task<List<PawnVsBankDifferenceDto>> GetPawnVsBankDifferenceReportAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var activePledges = await context.BankPledges
            .AsNoTracking()
            .Include(b => b.Bank)
            .Include(b => b.BankPledgeLoans).ThenInclude(bpl => bpl.Loan).ThenInclude(l => l.Customer)
            .Where(b => b.Status == BankPledgeStatus.Active || b.Status == BankPledgeStatus.PartiallyReleased)
            .ToListAsync(ct);

        var list = new List<PawnVsBankDifferenceDto>();

        foreach (var p in activePledges)
        {
            var unreleasedLoans = p.BankPledgeLoans.Where(l => !l.IsReleased).ToList();
            var totalCustAmt = unreleasedLoans.Sum(l => l.Loan.LoanAmount);

            foreach (var bpl in unreleasedLoans)
            {
                var proportion = totalCustAmt > 0 ? (bpl.Loan.LoanAmount / totalCustAmt) : 0m;
                var bankPortion = Math.Round(p.DisbursedAmount * proportion, 2);
                var diff = bpl.Loan.LoanAmount - bankPortion;

                list.Add(new PawnVsBankDifferenceDto(
                    p.Bank.Name,
                    p.PledgeNumber,
                    bpl.Loan.LoanNumber,
                    bpl.Loan.Customer.FullName,
                    bpl.Loan.NetWeight,
                    bpl.Loan.LoanAmount,
                    bankPortion,
                    diff
                ));
            }
        }

        return list;
    }

    public async Task<List<JewelCustodyStockDto>> GetJewelStockCustodyReportAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var activeLoans = await context.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.Items).ThenInclude(i => i.JewelType)
            .Include(l => l.BankPledgeLoans).ThenInclude(bpl => bpl.BankPledge).ThenInclude(bp => bp.Bank)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .ToListAsync(ct);

        var result = new List<JewelCustodyStockDto>();

        foreach (var loan in activeLoans)
        {
            var activeBankPledge = loan.BankPledgeLoans
                .FirstOrDefault(bpl => !bpl.IsReleased && bpl.BankPledge.Status != BankPledgeStatus.FullyReleased);

            var custody = activeBankPledge != null
                ? $"In Bank Custody ({activeBankPledge.BankPledge.Bank.Name} - #{activeBankPledge.BankPledge.PledgeNumber})"
                : "In Shop Locker";

            if (loan.Items.Count == 0)
            {
                result.Add(new JewelCustodyStockDto(
                    custody,
                    loan.LoanNumber,
                    loan.Customer.FullName,
                    "Gold Ornaments",
                    1,
                    loan.GrossWeight,
                    loan.NetWeight,
                    loan.MarketValue,
                    loan.PacketNumber ?? "-"
                ));
            }
            else
            {
                foreach (var item in loan.Items)
                {
                    result.Add(new JewelCustodyStockDto(
                        custody,
                        loan.LoanNumber,
                        loan.Customer.FullName,
                        $"{item.JewelType?.Name} - {item.Description}",
                        item.Quantity,
                        item.GrossWeight,
                        item.NetWeight,
                        item.AppraisedValue,
                        loan.PacketNumber ?? "-"
                    ));
                }
            }
        }

        return result.OrderBy(r => r.CustodyLocation).ThenBy(r => r.LoanNumber).ToList();
    }
}
