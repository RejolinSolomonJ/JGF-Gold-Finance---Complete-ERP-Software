using System.Text;
using JGF.Application.Receipts;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

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

    internal ReceiptService(
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

        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var isRelational = context.Database.IsRelational();
            await using var tx = isRelational ? await context.Database.BeginTransactionAsync(ct) : null;

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
        });
    }

    public async Task<byte[]> GenerateReceiptPdfAsync(Guid receiptId, bool isDuplicate = false, CancellationToken ct = default)
    {
        var receipt = await GetReceiptByIdAsync(receiptId, ct)
            ?? throw new ArgumentException("Receipt not found.");

        using var doc = new PdfDocument();
        doc.Info.Title = $"JGF Receipt #{receipt.ReceiptNumber}";
        doc.Info.Author = "Jeba Gold Finance";
        doc.Info.Subject = "Jewel Loan Payment Receipt";

        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(420); // A5 width (approx 148 mm)
        page.Height = XUnit.FromPoint(595); // A5 height (approx 210 mm)

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var fontCompany = new XFont("Arial", 15, XFontStyleEx.Bold);
            var fontSub = new XFont("Arial", 9, XFontStyleEx.Bold);
            var fontTitle = new XFont("Arial", 11, XFontStyleEx.Bold);
            var fontBold = new XFont("Arial", 9, XFontStyleEx.Bold);
            var fontRegular = new XFont("Arial", 9, XFontStyleEx.Regular);
            var fontSmall = new XFont("Arial", 8, XFontStyleEx.Regular);
            var fontTotal = new XFont("Arial", 13, XFontStyleEx.Bold);

            var primaryBlue = XColor.FromArgb(0x1E, 0x3A, 0x8A);
            var goldColor = XColor.FromArgb(0xB8, 0x86, 0x0B);
            var textDark = XColor.FromArgb(0x0F, 0x17, 0x2A);
            var lightGray = XColor.FromArgb(0x64, 0x74, 0x8B);
            var cardBg = XColor.FromArgb(0xF8, 0xFA, 0xFC);
            var borderPen = new XPen(XColor.FromArgb(0xCB, 0xD5, 0xE1), 1);
            var accentPen = new XPen(goldColor, 2);

            double left = 24;
            double right = page.Width.Point - 24;
            double width = right - left;
            double y = 24;

            // Company Header Banner
            gfx.DrawRectangle(new XSolidBrush(cardBg), left, y, width, 58);
            gfx.DrawRectangle(borderPen, left, y, width, 58);
            gfx.DrawRectangle(new XSolidBrush(primaryBlue), left, y, 6, 58);

            gfx.DrawString("JEBA GOLD FINANCE (JGF)", fontCompany, new XSolidBrush(primaryBlue), left + 14, y + 20);
            gfx.DrawString("JEWEL LOAN FINANCE & PLEDGE SERVICES", fontSub, new XSolidBrush(goldColor), left + 14, y + 36);
            gfx.DrawString("Main Road, Chennai | Helpline: 9876543210 | Reg: JGF-TN-2024", fontSmall, new XSolidBrush(lightGray), left + 14, y + 49);

            y += 68;

            // Receipt Title
            string title = isDuplicate ? "PAYMENT RECEIPT (DUPLICATE COPY)" : "JEWEL LOAN PAYMENT RECEIPT";
            gfx.DrawString(title, fontTitle, new XSolidBrush(primaryBlue), left, y);
            y += 16;
            gfx.DrawLine(accentPen, left, y, right, y);
            y += 12;

            // Metadata Block (2 Columns)
            double colMid = left + (width / 2);
            gfx.DrawString($"Receipt No : {receipt.ReceiptNumber}", fontBold, new XSolidBrush(textDark), left, y);
            gfx.DrawString($"Loan No  : {receipt.Loan.LoanNumber}", fontBold, new XSolidBrush(goldColor), colMid, y);
            y += 16;

            gfx.DrawString($"Date       : {receipt.ReceiptDate:dd-MMM-yyyy}", fontRegular, new XSolidBrush(textDark), left, y);
            gfx.DrawString($"Customer : {receipt.Loan.Customer.Name} ({receipt.Loan.Customer.Code})", fontRegular, new XSolidBrush(textDark), colMid, y);
            y += 16;

            gfx.DrawString($"Pay Mode   : {receipt.PaymentMode}", fontRegular, new XSolidBrush(textDark), left, y);
            gfx.DrawString($"Mobile   : {receipt.Loan.Customer.Phone}", fontRegular, new XSolidBrush(textDark), colMid, y);
            y += 16;

            gfx.DrawString($"Type       : {receipt.ReceiptType}", fontRegular, new XSolidBrush(textDark), left, y);
            gfx.DrawString($"Period   : {receipt.InterestCalculatedToDate:dd-MMM-yyyy} ({receipt.DaysCharged} days)", fontRegular, new XSolidBrush(textDark), colMid, y);
            y += 22;

            // Particulars Table Header
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0x1E, 0x3A, 0x8A)), left, y, width, 22);
            gfx.DrawString("PARTICULARS", fontSub, XBrushes.White, left + 8, y + 15);
            var formatRight = new XStringFormat { Alignment = XStringAlignment.Far };
            gfx.DrawString("AMOUNT (Rs)", fontSub, XBrushes.White, right - 8, y + 15, formatRight);
            y += 22;

            // Table Rows
            void DrawRow(string label, decimal amount, bool highlight = false)
            {
                var rowBg = highlight ? XColor.FromArgb(0xDC, 0xFC, 0xE7) : cardBg;
                gfx.DrawRectangle(new XSolidBrush(rowBg), left, y, width, 20);
                gfx.DrawRectangle(borderPen, left, y, width, 20);

                var f = highlight ? fontBold : fontRegular;
                var b = highlight ? new XSolidBrush(XColor.FromArgb(0x16, 0x65, 0x34)) : new XSolidBrush(textDark);

                gfx.DrawString(label, f, b, left + 8, y + 14);
                gfx.DrawString($"Rs {amount:N2}", f, b, right - 8, y + 14, formatRight);
                y += 20;
            }

            DrawRow("Principal Amount Paid", receipt.PrincipalAmount);
            DrawRow("Accrued Interest Paid", receipt.InterestAmount);
            if (receipt.PenalInterestAmount > 0)
                DrawRow("Penal Interest", receipt.PenalInterestAmount);
            if (receipt.PostalCharges > 0)
                DrawRow("Postal / Notice Charges", receipt.PostalCharges);
            if (receipt.OtherCharges > 0)
                DrawRow("Other Charges", receipt.OtherCharges);

            y += 6;

            // Total Block
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xDC, 0xFC, 0xE7)), left, y, width, 28);
            gfx.DrawRectangle(new XPen(XColor.FromArgb(0x16, 0xA3, 0x4A), 1.5), left, y, width, 28);
            gfx.DrawString("NET TOTAL RECEIVED:", fontTotal, new XSolidBrush(XColor.FromArgb(0x15, 0x80, 0x3D)), left + 8, y + 19);
            gfx.DrawString($"Rs {receipt.TotalAmount:N2}", fontTotal, new XSolidBrush(XColor.FromArgb(0x15, 0x80, 0x3D)), right - 8, y + 19, formatRight);

            y += 38;

            // Terms Notice
            gfx.DrawRectangle(new XSolidBrush(cardBg), left, y, width, 40);
            gfx.DrawRectangle(borderPen, left, y, width, 40);
            gfx.DrawString("• Pledged gold ornaments remain securely insured in Bank/Vault lockers.", fontSmall, new XSolidBrush(lightGray), left + 6, y + 14);
            gfx.DrawString("• Ornaments will be released only upon full settlement & original ticket presentation.", fontSmall, new XSolidBrush(lightGray), left + 6, y + 26);
            gfx.DrawString("• This is a computer generated receipt. Retain for all future transactions.", fontSmall, new XSolidBrush(lightGray), left + 6, y + 36);

            y += 65;

            // Signatures
            gfx.DrawLine(borderPen, left, y, left + 140, y);
            gfx.DrawLine(borderPen, right - 140, y, right, y);
            gfx.DrawString("Authorized Cashier / JGF", fontBold, new XSolidBrush(textDark), left + 10, y + 14);
            gfx.DrawString("Customer / Payer Signature", fontBold, new XSolidBrush(textDark), right - 135, y + 14);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
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

        using var doc = new PdfDocument();
        doc.Info.Title = $"JGF Release Slip #{loan.LoanNumber}";
        doc.Info.Author = "Jeba Gold Finance";

        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(420);
        page.Height = XUnit.FromPoint(595);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var fontCompany = new XFont("Arial", 15, XFontStyleEx.Bold);
            var fontSub = new XFont("Arial", 9, XFontStyleEx.Bold);
            var fontTitle = new XFont("Arial", 11, XFontStyleEx.Bold);
            var fontBold = new XFont("Arial", 9, XFontStyleEx.Bold);
            var fontRegular = new XFont("Arial", 9, XFontStyleEx.Regular);
            var fontSmall = new XFont("Arial", 8, XFontStyleEx.Regular);
            var fontTotal = new XFont("Arial", 12, XFontStyleEx.Bold);

            var primaryBlue = XColor.FromArgb(0x1E, 0x3A, 0x8A);
            var goldColor = XColor.FromArgb(0xB8, 0x86, 0x0B);
            var textDark = XColor.FromArgb(0x0F, 0x17, 0x2A);
            var lightGray = XColor.FromArgb(0x64, 0x74, 0x8B);
            var cardBg = XColor.FromArgb(0xF8, 0xFA, 0xFC);
            var borderPen = new XPen(XColor.FromArgb(0xCB, 0xD5, 0xE1), 1);
            var accentPen = new XPen(goldColor, 2);

            double left = 24;
            double right = page.Width.Point - 24;
            double width = right - left;
            double y = 24;

            // Company Header
            gfx.DrawRectangle(new XSolidBrush(cardBg), left, y, width, 56);
            gfx.DrawRectangle(borderPen, left, y, width, 56);
            gfx.DrawRectangle(new XSolidBrush(primaryBlue), left, y, 6, 56);

            gfx.DrawString("JEBA GOLD FINANCE (JGF)", fontCompany, new XSolidBrush(primaryBlue), left + 14, y + 20);
            gfx.DrawString("ORNAMENT RELEASE SLIP", fontSub, new XSolidBrush(goldColor), left + 14, y + 36);
            gfx.DrawString("Main Road, Chennai | Helpline: 9876543210", fontSmall, new XSolidBrush(lightGray), left + 14, y + 48);

            y += 66;

            gfx.DrawString("LOAN CLOSURE & JEWEL DISCHARGE VOUCHER", fontTitle, new XSolidBrush(primaryBlue), left, y);
            y += 16;
            gfx.DrawLine(accentPen, left, y, right, y);
            y += 12;

            double colMid = left + (width / 2);
            gfx.DrawString($"Loan Number : {loan.LoanNumber}", fontBold, new XSolidBrush(goldColor), left, y);
            gfx.DrawString($"Status   : FULLY REDEEMED (CLOSED)", fontBold, new XSolidBrush(XColor.FromArgb(0x15, 0x80, 0x3D)), colMid, y);
            y += 16;

            gfx.DrawString($"Close Date  : {loan.ClosedDate:dd-MMM-yyyy}", fontRegular, new XSolidBrush(textDark), left, y);
            gfx.DrawString($"Customer : {loan.Customer.Name} ({loan.Customer.Code})", fontRegular, new XSolidBrush(textDark), colMid, y);
            y += 16;

            gfx.DrawString($"Aadhaar     : {loan.Customer.AadhaarNumber ?? "Verified"}", fontRegular, new XSolidBrush(textDark), left, y);
            gfx.DrawString($"Mobile   : {loan.Customer.Phone}", fontRegular, new XSolidBrush(textDark), colMid, y);
            y += 24;

            // Jewel Handover Table Header
            gfx.DrawRectangle(new XSolidBrush(primaryBlue), left, y, width, 22);
            gfx.DrawString("ORNAMENTS HANDED OVER", fontSub, XBrushes.White, left + 8, y + 15);
            var formatRight = new XStringFormat { Alignment = XStringAlignment.Far };
            gfx.DrawString("NET WT (g)", fontSub, XBrushes.White, right - 8, y + 15, formatRight);
            y += 22;

            int idx = 1;
            foreach (var item in loan.Items)
            {
                gfx.DrawRectangle(new XSolidBrush(cardBg), left, y, width, 20);
                gfx.DrawRectangle(borderPen, left, y, width, 20);
                string desc = $"{idx++}. {item.JewelType?.Name ?? "Gold"} - {item.Description} (Qty: {item.Quantity})";
                gfx.DrawString(desc, fontRegular, new XSolidBrush(textDark), left + 8, y + 14);
                gfx.DrawString($"{item.NetWeight:F3} g  [✓]", fontBold, new XSolidBrush(primaryBlue), right - 8, y + 14, formatRight);
                y += 20;
            }

            y += 8;
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xDC, 0xFC, 0xE7)), left, y, width, 26);
            gfx.DrawRectangle(new XPen(XColor.FromArgb(0x16, 0xA3, 0x4A), 1), left, y, width, 26);
            gfx.DrawString("TOTAL NET WEIGHT DELIVERED:", fontTotal, new XSolidBrush(XColor.FromArgb(0x15, 0x80, 0x3D)), left + 8, y + 18);
            gfx.DrawString($"{loan.NetWeight:F3} grams", fontTotal, new XSolidBrush(XColor.FromArgb(0x15, 0x80, 0x3D)), right - 8, y + 18, formatRight);

            y += 38;

            gfx.DrawRectangle(new XSolidBrush(cardBg), left, y, width, 44);
            gfx.DrawRectangle(borderPen, left, y, width, 44);
            gfx.DrawString("Borrower Acknowledgment:", fontBold, new XSolidBrush(textDark), left + 6, y + 14);
            gfx.DrawString("I hereby confirm that I have inspected and received all the pledged ornaments back", fontSmall, new XSolidBrush(textDark), left + 6, y + 26);
            gfx.DrawString("in safe, undamaged and complete condition. No claim remains outstanding against JGF.", fontSmall, new XSolidBrush(textDark), left + 6, y + 36);

            y += 66;

            gfx.DrawLine(borderPen, left, y, left + 140, y);
            gfx.DrawLine(borderPen, right - 140, y, right, y);
            gfx.DrawString("Appraiser / Cashier Signature", fontBold, new XSolidBrush(textDark), left + 6, y + 14);
            gfx.DrawString("Customer / Borrower Signature", fontBold, new XSolidBrush(textDark), right - 135, y + 14);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }
}
