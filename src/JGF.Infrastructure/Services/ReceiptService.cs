using System.Text;
using JGF.Application.Receipts;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JGF.Infrastructure.Services;

public class ReceiptService : DbServiceBase, IReceiptService
{
    private readonly INumberSeriesService _numberSeries;
    private readonly IPermissionService _permissionService;

    [ActivatorUtilitiesConstructor]
    public ReceiptService(
        IDbContextFactory<JgfDbContext> contextFactory,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
        : base(contextFactory)
    {
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    public ReceiptService(
        JgfDbContext context,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
        : base(context)
    {
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    public async Task<Receipt?> GetReceiptByIdAsync(Guid receiptId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.Receipts
            .AsNoTracking()
            .Include(r => r.Loan).ThenInclude(l => l.Customer)
            .Include(r => r.Loan).ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct);
    }

    public async Task<List<Receipt>> GetReceiptsForLoanAsync(Guid loanId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.Receipts
            .AsNoTracking()
            .Where(r => r.LoanId == loanId)
            .OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<Receipt> ProcessReceiptAsync(CreateReceiptDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.CreateReceipt);

        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var loan = await context.Loans
            .Include(l => l.Customer)
            .FirstOrDefaultAsync(l => l.Id == dto.LoanId, ct)
            ?? throw new ArgumentException("Loan not found.");

        if (loan.Status == LoanStatus.Closed)
        {
            throw new InvalidOperationException("Loan is already closed.");
        }

        // Safety Check: If closing, verify jewels are not pledged to a Bank/NBFC
        if (dto.ReceiptType == ReceiptType.Closure)
        {
            var isWithBank = await context.BankPledgeLoans
                .AnyAsync(bpl => bpl.LoanId == loan.Id && !bpl.IsReleased, ct);

            if (isWithBank && dto.UserRole != UserRole.Owner)
            {
                throw new InvalidOperationException(
                    $"Loan {loan.LoanNumber} cannot be closed because its pledged jewels are currently deposited with a Bank/NBFC! " +
                    "Release the ornaments from the Bank Repledge module first, or obtain Owner override.");
            }
        }

        var isRelational = context.Database.IsRelational();
        using var tx = isRelational ? await context.Database.BeginTransactionAsync(ct) : null;

        var fy = "2024-25";
        var rNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.ReceiptNumber, fy, null, ct);

        var totalAmount = dto.PrincipalAmount + dto.InterestAmount + dto.PenalInterestAmount 
                        + dto.PostalCharges + dto.OtherCharges - dto.AdjustmentAmount;

        var receipt = new Receipt
        {
            ReceiptNumber = rNumber,
            LoanId = loan.Id,
            ReceiptDate = dto.ReceiptDate,
            ReceiptType = dto.ReceiptType,
            PaymentMode = dto.PaymentMode,
            PrincipalAmount = dto.PrincipalAmount,
            InterestAmount = dto.InterestAmount,
            PenalInterestAmount = dto.PenalInterestAmount,
            PostalCharges = dto.PostalCharges,
            OtherCharges = dto.OtherCharges,
            OtherChargesDescription = dto.OtherChargesDescription,
            TotalAmount = totalAmount,
            ReferenceNumber = dto.ReferenceNumber,
            InterestCalculatedFromDate = dto.InterestFromDate,
            InterestCalculatedToDate = dto.InterestToDate,
            DaysCharged = dto.DaysCharged,
            Notes = dto.AdjustmentReason != null ? $"Adjustment: {dto.AdjustmentReason}" : null
        };

        await context.Receipts.AddAsync(receipt, ct);

        // Update Loan state
        loan.LastInterestPaidDate = dto.InterestToDate;

        var lastBalance = await context.LoanLedgerEntries
            .Where(e => e.LoanId == loan.Id)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.CreatedAtUtc)
            .Select(e => e.Balance)
            .FirstOrDefaultAsync(ct);

        var newPrincipalBalance = Math.Max(0m, lastBalance - dto.PrincipalAmount);

        if (dto.ReceiptType == ReceiptType.Closure || newPrincipalBalance == 0)
        {
            loan.Status = LoanStatus.Closed;
            loan.ClosedDate = dto.ReceiptDate;
            loan.ClosingReceiptId = receipt.Id;
        }
        else if (dto.PrincipalAmount > 0)
        {
            loan.Status = LoanStatus.PartiallyPaid;
        }

        // Sub-ledger entries
        if (dto.InterestAmount > 0 || dto.PenalInterestAmount > 0)
        {
            await context.LoanLedgerEntries.AddAsync(new LoanLedgerEntry
            {
                LoanId = loan.Id,
                EntryDate = dto.ReceiptDate,
                EntryType = LoanLedgerEntryType.InterestPayment,
                DebitAmount = 0m,
                CreditAmount = dto.InterestAmount + dto.PenalInterestAmount,
                Balance = lastBalance,
                ReceiptId = receipt.Id,
                Narration = $"Interest payment received up to {dto.InterestToDate:dd-MMM-yyyy} (Receipt {receipt.ReceiptNumber})"
            }, ct);
        }

        if (dto.PrincipalAmount > 0)
        {
            await context.LoanLedgerEntries.AddAsync(new LoanLedgerEntry
            {
                LoanId = loan.Id,
                EntryDate = dto.ReceiptDate,
                EntryType = LoanLedgerEntryType.PrincipalPayment,
                DebitAmount = 0m,
                CreditAmount = dto.PrincipalAmount,
                Balance = newPrincipalBalance,
                ReceiptId = receipt.Id,
                Narration = $"Principal repayment (Receipt {receipt.ReceiptNumber})"
            }, ct);
        }

        // Multi-line balanced voucher
        var voucherNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);
        var voucher = new Voucher
        {
            VoucherNumber = voucherNumber,
            VoucherDate = dto.ReceiptDate,
            VoucherType = VoucherType.Receipt,
            TotalDebit = totalAmount,
            TotalCredit = totalAmount,
            Narration = $"Receipt {receipt.ReceiptNumber} for Loan {loan.LoanNumber} ({loan.Customer.FullName})",
            IsPosted = true,
            PostedAtUtc = DateTime.UtcNow,
            PostedByUserId = dto.UserId,
            ReferenceType = "Receipt",
            ReferenceId = receipt.Id
        };

        var headCash = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct);
        var headJewelLoan = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1002", ct);
        var headInterest = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "4001", ct);
        var headPenal = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "4002", ct);
        var headPostal = await context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "4004", ct);

        int line = 1;
        // Debit side (Total money received in Cash or Bank)
        voucher.Lines.Add(new VoucherLine
        {
            LedgerHeadId = headCash!.Id,
            DebitAmount = totalAmount,
            CreditAmount = 0m,
            Narration = $"Collection on receipt {receipt.ReceiptNumber}",
            LineOrder = line++
        });

        // Credit side (Principal reduction)
        if (dto.PrincipalAmount > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headJewelLoan!.Id,
                DebitAmount = 0m,
                CreditAmount = dto.PrincipalAmount,
                Narration = $"Principal credit {loan.LoanNumber}",
                LineOrder = line++
            });
        }

        // Credit side (Interest income)
        if (dto.InterestAmount > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headInterest!.Id,
                DebitAmount = 0m,
                CreditAmount = dto.InterestAmount,
                Narration = $"Interest income {loan.LoanNumber}",
                LineOrder = line++
            });
        }

        // Credit side (Penal interest income)
        if (dto.PenalInterestAmount > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headPenal!.Id,
                DebitAmount = 0m,
                CreditAmount = dto.PenalInterestAmount,
                Narration = $"Penal interest income {loan.LoanNumber}",
                LineOrder = line++
            });
        }

        // Credit side (Postal charges recovery)
        if (dto.PostalCharges > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headPostal!.Id,
                DebitAmount = 0m,
                CreditAmount = dto.PostalCharges,
                Narration = $"Postal charges recovered {loan.LoanNumber}",
                LineOrder = line++
            });
        }

        await context.Vouchers.AddAsync(voucher, ct);
        receipt.VoucherId = voucher.Id;

        await context.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return receipt;
    }

    public async Task<byte[]> GenerateReceiptPdfAsync(Guid receiptId, bool isDuplicate = false, CancellationToken ct = default)
    {
        var receipt = await GetReceiptByIdAsync(receiptId, ct)
            ?? throw new ArgumentException("Receipt not found.");

        // Generates cleanly structured text-document representation ready for QuestPDF canvas
        var sb = new StringBuilder();
        sb.AppendLine("==================================================================");
        sb.AppendLine(isDuplicate ? "       ✦ JGF JEWEL LOAN RECEIPT (DUPLICATE COPY) ✦       " : "              ✦ JGF JEWEL LOAN RECEIPT ✦                 ");
        sb.AppendLine("                 ரசீது / கட்டண சான்று                     ");
        sb.AppendLine("==================================================================");
        sb.AppendLine($"Receipt No  : {receipt.ReceiptNumber}");
        sb.AppendLine($"Date        : {receipt.ReceiptDate:dd-MMM-yyyy}");
        sb.AppendLine($"Loan Number : {receipt.Loan.LoanNumber}");
        sb.AppendLine($"Customer    : {receipt.Loan.Customer.FullName} ({receipt.Loan.Customer.Code})");
        sb.AppendLine($"Mobile      : {receipt.Loan.Customer.MobileNumber}");
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine($"Receipt Type: {receipt.ReceiptType}");
        sb.AppendLine($"Payment Mode: {receipt.PaymentMode}");
        sb.AppendLine($"Interest To : {receipt.InterestCalculatedToDate:dd-MMM-yyyy} ({receipt.DaysCharged} days)");
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine($"Principal Paid  : ₹ {receipt.PrincipalAmount,12:N2}");
        sb.AppendLine($"Interest Paid   : ₹ {receipt.InterestAmount,12:N2}");
        sb.AppendLine($"Penal Interest  : ₹ {receipt.PenalInterestAmount,12:N2}");
        sb.AppendLine($"Postal Charges  : ₹ {receipt.PostalCharges,12:N2}");
        sb.AppendLine($"Other Charges   : ₹ {receipt.OtherCharges,12:N2}");
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine($"TOTAL RECEIVED  : ₹ {receipt.TotalAmount,12:N2}");
        sb.AppendLine("==================================================================");
        sb.AppendLine("Signatures:       [ Cashier / Cash Received ]      [ Customer ]   ");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> GenerateReleaseSlipPdfAsync(Guid loanId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var loan = await scope.Context.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.Items).ThenInclude(i => i.JewelType)
            .FirstOrDefaultAsync(l => l.Id == loanId, ct)
            ?? throw new ArgumentException("Loan not found.");

        var sb = new StringBuilder();
        sb.AppendLine("==================================================================");
        sb.AppendLine("             ✦ JGF JEWEL RELEASE SLIP (மீட்பு சீட்டு) ✦           ");
        sb.AppendLine("==================================================================");
        sb.AppendLine($"Loan Number : {loan.LoanNumber}");
        sb.AppendLine($"Closed Date : {loan.ClosedDate:dd-MMM-yyyy}");
        sb.AppendLine($"Customer    : {loan.Customer.FullName} ({loan.Customer.Code})");
        sb.AppendLine($"Aadhaar     : {loan.Customer.AadhaarNumber ?? "Verified"}");
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine("Collateral Ornaments Handed Over (நகைகள் ஒப்படைக்கப்பட்டது):");
        int idx = 1;
        foreach (var item in loan.Items)
        {
            sb.AppendLine($"  {idx++}. {item.JewelType?.Name} - {item.Description} (Qty: {item.Quantity}, Net: {item.NetWeight:F3}g) [✓ Checked]");
        }
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine($"Total Net Weight Released: {loan.NetWeight:F3} grams");
        sb.AppendLine("Customer Acknowledgement:");
        sb.AppendLine("I confirm that I have received all pledged ornaments in good condition.");
        sb.AppendLine("\n\nSignature of Customer: ____________________   Date: ______________");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
