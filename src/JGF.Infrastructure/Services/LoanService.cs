using JGF.Application.Loans;
using JGF.Application.Receipts;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class LoanService : ILoanService
{
    private readonly JgfDbContext _context;
    private readonly INumberSeriesService _numberSeries;
    private readonly IPermissionService _permissionService;

    public LoanService(
        JgfDbContext context,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
    {
        _context = context;
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    public async Task<Loan?> GetLoanByIdAsync(Guid loanId, CancellationToken ct = default)
    {
        return await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .Include(l => l.Items).ThenInclude(i => i.JewelType)
            .Include(l => l.Receipts)
            .Include(l => l.LedgerEntries)
            .FirstOrDefaultAsync(l => l.Id == loanId, ct);
    }

    public async Task<Loan?> FindLoanByNumberOrBarcodeAsync(string query, CancellationToken ct = default)
    {
        var q = query.Trim().ToUpper();
        return await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .Include(l => l.Items).ThenInclude(i => i.JewelType)
            .FirstOrDefaultAsync(l => l.LoanNumber.ToUpper() == q || l.PacketNumber == q, ct);
    }

    public async Task<List<Loan>> SearchLoansAsync(string query, int take = 50, CancellationToken ct = default)
    {
        var q = query.Trim().ToLower();
        return await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .Where(l => l.LoanNumber.ToLower().Contains(q) ||
                        l.Customer.FullName.ToLower().Contains(q) ||
                        l.Customer.MobileNumber.Contains(q) ||
                        (l.PacketNumber != null && l.PacketNumber.ToLower().Contains(q)))
            .OrderByDescending(l => l.PledgeDate)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<Loan> IssueNewLoanAsync(CreateLoanDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.CreateLoan);

        // Check back-dated rule: Back-dated entries are Owner only
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (dto.PledgeDate < today && dto.UserRole != UserRole.Owner)
        {
            throw new UnauthorizedAccessException("Back-dated loan pledge is restricted to the Owner role only.");
        }

        var scheme = await _context.Schemes.FindAsync(new object[] { dto.SchemeId }, ct)
            ?? throw new ArgumentException("Selected scheme does not exist.");

        var customer = await _context.Customers.FindAsync(new object[] { dto.CustomerId }, ct)
            ?? throw new ArgumentException("Selected customer does not exist.");

        // Calculate totals from items
        decimal totalGross = dto.Items.Sum(i => i.GrossWeight);
        decimal totalDeduction = dto.Items.Sum(i => i.DeductionWeight);
        decimal totalNet = dto.Items.Sum(i => i.NetWeight);
        decimal totalAppraisedValue = dto.Items.Sum(i => i.AppraisedValue);

        // Max eligible LTV check
        var maxEligible = Math.Round(totalAppraisedValue * (scheme.MaxLtvPercentage / 100m), 2);
        if (dto.LoanAmount > maxEligible)
        {
            if (dto.UserRole != UserRole.Owner)
            {
                throw new InvalidOperationException($"Loan amount ₹{dto.LoanAmount:N2} exceeds scheme LTV limit (Max ₹{maxEligible:N2}). Owner authorization required.");
            }
            if (string.IsNullOrWhiteSpace(dto.OwnerOverrideReason))
            {
                throw new InvalidOperationException("Owner override reason is mandatory when exceeding scheme LTV limit.");
            }
        }

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        // 1. Issue unique gapless loan number
        var fy = "2024-25"; // Active financial year
        var loanNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.LoanNumber, fy, scheme.Id, ct);

        // Default packet number if empty
        var packet = string.IsNullOrWhiteSpace(dto.PacketNumber)
            ? $"PKT-{loanNumber.Replace("/", "-")}"
            : dto.PacketNumber.Trim();

        var loan = new Loan
        {
            LoanNumber = loanNumber,
            CustomerId = dto.CustomerId,
            SchemeId = dto.SchemeId,
            PledgeDate = dto.PledgeDate,
            MaturityDate = dto.PledgeDate.AddMonths(scheme.TenureMonths),
            LoanAmount = dto.LoanAmount,
            MarketValue = totalAppraisedValue,
            NetWeight = totalNet,
            GrossWeight = totalGross,
            DeductionWeight = totalDeduction,
            ApplicableGoldRate = dto.Items.FirstOrDefault()?.RatePerGram ?? 0m,
            Status = LoanStatus.Active,
            NomineeName = dto.NomineeName,
            NomineeRelationship = dto.NomineeRelationship,
            PacketNumber = packet,
            JewelPhotoPath = dto.JewelPhotoPath,
            OwnerLtvOverrideReason = dto.OwnerOverrideReason,
            LastInterestPaidDate = dto.PledgeDate
        };

        await _context.Loans.AddAsync(loan, ct);

        // 2. Add LoanItems
        int itemIndex = 1;
        foreach (var itemDto in dto.Items)
        {
            var item = new LoanItem
            {
                LoanId = loan.Id,
                JewelTypeId = itemDto.JewelTypeId,
                Description = itemDto.Description,
                Quantity = itemDto.Quantity,
                GrossWeight = itemDto.GrossWeight,
                StoneWeight = itemDto.DeductionWeight,
                NetWeight = itemDto.NetWeight,
                PurityPercentage = itemDto.PurityPercent,
                AppraisedValue = itemDto.AppraisedValue,
                MarketValue = itemDto.AppraisedValue
            };
            await _context.LoanItems.AddAsync(item, ct);
            itemIndex++;
        }

        // 3. Create LoanLedgerEntry for Disbursement
        var ledgerEntry = new LoanLedgerEntry
        {
            LoanId = loan.Id,
            EntryDate = dto.PledgeDate,
            EntryType = LoanLedgerEntryType.Disbursement,
            DebitAmount = dto.LoanAmount,
            CreditAmount = 0m,
            Balance = dto.LoanAmount,
            Narration = $"Gold loan disbursement against {loan.LoanNumber} (Net: {totalNet:F3}g)"
        };
        await _context.LoanLedgerEntries.AddAsync(ledgerEntry, ct);

        // 4. Double-Entry Accounting Voucher:
        // Debit: Jewel Loan Receivable (1002)
        // Credit: Cash on Hand (1001)
        var headJewelLoan = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1002", ct);
        var headCash = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct);

        if (headJewelLoan != null && headCash != null)
        {
            var voucherNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);
            var voucher = new Voucher
            {
                VoucherNumber = voucherNumber,
                VoucherDate = dto.PledgeDate,
                VoucherType = VoucherType.Payment,
                TotalDebit = dto.LoanAmount,
                TotalCredit = dto.LoanAmount,
                Narration = $"Disbursement for Loan {loan.LoanNumber} to {customer.FullName}",
                IsPosted = true,
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = dto.UserId,
                ReferenceType = "Loan",
                ReferenceId = loan.Id
            };

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headJewelLoan.Id,
                DebitAmount = dto.LoanAmount,
                CreditAmount = 0m,
                Narration = $"Loan disbursed {loan.LoanNumber}",
                LineOrder = 1
            });

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headCash.Id,
                DebitAmount = 0m,
                CreditAmount = dto.LoanAmount,
                Narration = $"Cash paid for loan {loan.LoanNumber}",
                LineOrder = 2
            });

            await _context.Vouchers.AddAsync(voucher, ct);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return loan;
    }

    public async Task<Loan> TopUpLoanAsync(TopUpLoanDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.EditLoan);

        var loan = await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .FirstOrDefaultAsync(l => l.Id == dto.LoanId, ct)
            ?? throw new ArgumentException("Loan not found.");

        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.PartiallyPaid)
        {
            throw new InvalidOperationException("Top-up is only allowed on active loans.");
        }

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        loan.LoanAmount += dto.TopUpAmount;

        // Sub-ledger
        var lastBalance = await _context.LoanLedgerEntries
            .Where(e => e.LoanId == loan.Id)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.CreatedAtUtc)
            .Select(e => e.Balance)
            .FirstOrDefaultAsync(ct);

        var newBalance = lastBalance + dto.TopUpAmount;

        var ledgerEntry = new LoanLedgerEntry
        {
            LoanId = loan.Id,
            EntryDate = DateOnly.FromDateTime(DateTime.Today),
            EntryType = LoanLedgerEntryType.Disbursement,
            DebitAmount = dto.TopUpAmount,
            CreditAmount = 0m,
            Balance = newBalance,
            Narration = $"Top-up loan disbursement: ₹{dto.TopUpAmount:N2}. Reason: {dto.Reason ?? "Market appreciation"}"
        };
        await _context.LoanLedgerEntries.AddAsync(ledgerEntry, ct);

        // Voucher
        var fy = "2024-25";
        var headJewelLoan = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1002", ct);
        var headCash = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct);

        if (headJewelLoan != null && headCash != null)
        {
            var voucherNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);
            var voucher = new Voucher
            {
                VoucherNumber = voucherNumber,
                VoucherDate = DateOnly.FromDateTime(DateTime.Today),
                VoucherType = VoucherType.Payment,
                TotalDebit = dto.TopUpAmount,
                TotalCredit = dto.TopUpAmount,
                Narration = $"Top-up on Loan {loan.LoanNumber}",
                IsPosted = true,
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = dto.UserId,
                ReferenceType = "LoanTopUp",
                ReferenceId = loan.Id
            };

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headJewelLoan.Id,
                DebitAmount = dto.TopUpAmount,
                CreditAmount = 0m,
                Narration = $"Top-up {loan.LoanNumber}",
                LineOrder = 1
            });

            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headCash.Id,
                DebitAmount = 0m,
                CreditAmount = dto.TopUpAmount,
                Narration = $"Cash paid for top-up {loan.LoanNumber}",
                LineOrder = 2
            });

            await _context.Vouchers.AddAsync(voucher, ct);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return loan;
    }

    public async Task<(Receipt ClosingReceipt, Loan NewLoan)> RenewLoanAsync(RenewalDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.CreateLoan);

        var oldLoan = await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == dto.ExistingLoanId, ct)
            ?? throw new ArgumentException("Existing loan not found.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        // 1. Create renewal closure receipt for the old loan
        var fy = "2024-25";
        var rNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.ReceiptNumber, fy, null, ct);

        var closingReceipt = new Receipt
        {
            ReceiptNumber = rNumber,
            LoanId = oldLoan.Id,
            ReceiptDate = dto.RenewalDate,
            ReceiptType = ReceiptType.Renewal,
            PaymentMode = PaymentMode.Cash,
            InterestAmount = dto.InterestPaidAmount,
            PrincipalAmount = 0m, // Principal carried forward
            TotalAmount = dto.InterestPaidAmount,
            InterestCalculatedFromDate = oldLoan.LastInterestPaidDate ?? oldLoan.PledgeDate,
            InterestCalculatedToDate = dto.RenewalDate,
            Notes = "Loan renewal: interest settled, principal rolled into new loan"
        };
        await _context.Receipts.AddAsync(closingReceipt, ct);

        // 2. Mark old loan closed
        oldLoan.Status = LoanStatus.Closed;
        oldLoan.ClosedDate = dto.RenewalDate;
        oldLoan.ClosingReceiptId = closingReceipt.Id;

        // 3. Issue new loan under selected scheme
        var newScheme = await _context.Schemes.FindAsync(new object[] { dto.NewSchemeId }, ct)
            ?? throw new ArgumentException("New scheme not found.");

        var newPrincipal = dto.AdjustedPrincipal ?? oldLoan.LoanAmount;
        var newLoanNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.LoanNumber, fy, newScheme.Id, ct);

        var newLoan = new Loan
        {
            LoanNumber = newLoanNumber,
            CustomerId = oldLoan.CustomerId,
            SchemeId = dto.NewSchemeId,
            PledgeDate = dto.RenewalDate,
            MaturityDate = dto.RenewalDate.AddMonths(newScheme.TenureMonths),
            LoanAmount = newPrincipal,
            MarketValue = oldLoan.MarketValue,
            NetWeight = oldLoan.NetWeight,
            GrossWeight = oldLoan.GrossWeight,
            DeductionWeight = oldLoan.DeductionWeight,
            ApplicableGoldRate = oldLoan.ApplicableGoldRate,
            Status = LoanStatus.Active,
            PacketNumber = oldLoan.PacketNumber,
            RenewalLoanId = oldLoan.Id,
            LastInterestPaidDate = dto.RenewalDate,
            Notes = $"Renewed from previous loan {oldLoan.LoanNumber}"
        };
        await _context.Loans.AddAsync(newLoan, ct);

        // Copy collateral items
        foreach (var oldItem in oldLoan.Items)
        {
            var newItem = new LoanItem
            {
                LoanId = newLoan.Id,
                JewelTypeId = oldItem.JewelTypeId,
                Description = oldItem.Description,
                Quantity = oldItem.Quantity,
                GrossWeight = oldItem.GrossWeight,
                StoneWeight = oldItem.StoneWeight,
                NetWeight = oldItem.NetWeight,
                PurityPercentage = oldItem.PurityPercentage,
                AppraisedValue = oldItem.AppraisedValue,
                MarketValue = oldItem.MarketValue
            };
            await _context.LoanItems.AddAsync(newItem, ct);
        }

        // Sub-ledger entries
        await _context.LoanLedgerEntries.AddAsync(new LoanLedgerEntry
        {
            LoanId = oldLoan.Id,
            EntryDate = dto.RenewalDate,
            EntryType = LoanLedgerEntryType.Closure,
            DebitAmount = 0m,
            CreditAmount = oldLoan.LoanAmount,
            Balance = 0m,
            Narration = $"Rolled over to new loan {newLoan.LoanNumber}"
        }, ct);

        await _context.LoanLedgerEntries.AddAsync(new LoanLedgerEntry
        {
            LoanId = newLoan.Id,
            EntryDate = dto.RenewalDate,
            EntryType = LoanLedgerEntryType.Disbursement,
            DebitAmount = newPrincipal,
            CreditAmount = 0m,
            Balance = newPrincipal,
            Narration = $"Disbursed from renewal of {oldLoan.LoanNumber}"
        }, ct);

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (closingReceipt, newLoan);
    }

    public async Task<List<LoanLedgerEntry>> GetLoanHistoryAsync(Guid loanId, CancellationToken ct = default)
    {
        return await _context.LoanLedgerEntries
            .Where(e => e.LoanId == loanId)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
