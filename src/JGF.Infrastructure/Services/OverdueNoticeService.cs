using System.Text;
using JGF.Application.Loans;
using JGF.Application.Notices;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class OverdueNoticeService : IOverdueNoticeService
{
    private readonly JgfDbContext _context;
    private readonly IInterestCalculator _calculator;
    private readonly INumberSeriesService _numberSeries;
    private readonly IPermissionService _permissionService;

    public OverdueNoticeService(
        JgfDbContext context,
        IInterestCalculator calculator,
        INumberSeriesService numberSeries,
        IPermissionService permissionService)
    {
        _context = context;
        _calculator = calculator;
        _numberSeries = numberSeries;
        _permissionService = permissionService;
    }

    public async Task<List<OverdueLoanSummaryDto>> GetOverdueLoansAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Fetch latest gold rate
        var todayGoldRate = await _context.GoldRates
            .OrderByDescending(g => g.Date)
            .FirstOrDefaultAsync(ct);
        var currentRate22K = todayGoldRate?.Rate22K ?? 5500.00m;

        var activeLoans = await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .Include(l => l.Items)
            .Include(l => l.Notices)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .ToListAsync(ct);

        var list = new List<OverdueLoanSummaryDto>();

        foreach (var loan in activeLoans)
        {
            var refDate = loan.LastInterestPaidDate ?? loan.PledgeDate;
            var elapsedDays = today.DayNumber - loan.PledgeDate.DayNumber;
            var elapsedMonths = (int)Math.Floor(elapsedDays / 30.0);

            var calc = _calculator.CalculateInterest(
                loan.LoanAmount,
                loan.Scheme?.InterestRatePerMonth ?? 1.50m,
                loan.Scheme?.PenalInterestRatePerMonth ?? 0.50m,
                loan.PledgeDate,
                today,
                loan.LastInterestPaidDate,
                loan.Scheme?.MaxTenureMonths ?? 12,
                loan.Scheme?.GraceDaysAfterExpiry ?? 7,
                loan.Scheme?.MinimumInterestDays ?? 30,
                roundUpToNextMonth: false
            );

            // Re-value gold at current daily market rate
            var currentGoldVal = loan.NetWeight * currentRate22K;
            var totalOutstanding = loan.LoanAmount + calc.TotalInterest;
            var currentLtv = currentGoldVal > 0 ? Math.Round((totalOutstanding / currentGoldVal) * 100m, 2) : 100m;

            var bucket = OverdueBucket.Normal;
            if (elapsedMonths >= 12 || today > loan.MaturityDate)
            {
                bucket = OverdueBucket.TwelveMonthMatured;
            }
            else if (elapsedMonths >= 6)
            {
                bucket = OverdueBucket.SixMonthReminder;
            }

            if (currentLtv >= 90m)
            {
                bucket = OverdueBucket.OverdueCritical;
            }

            var noticesSent = loan.Notices.Count;
            var lastNoticeDate = loan.Notices.OrderByDescending(n => n.NoticeDate).FirstOrDefault()?.NoticeDate;

            // Legally eligible for auction if:
            // 1. Matured (12+ months or past maturity date)
            // 2. Final/Auction Notice was served at least 15 days ago
            var hasServedAuctionNotice = loan.Notices.Any(n => 
                (n.NoticeType == NoticeType.AuctionNotice || n.NoticeType == NoticeType.FinalNotice) &&
                n.NoticeDate.AddDays(15) <= today);

            var isAuctionEligible = (bucket == OverdueBucket.TwelveMonthMatured || bucket == OverdueBucket.OverdueCritical) 
                                    && hasServedAuctionNotice;

            list.Add(new OverdueLoanSummaryDto(
                loan.Id,
                loan.LoanNumber,
                loan.Customer?.FullName ?? "-",
                loan.Customer?.MobileNumber ?? "-",
                loan.PledgeDate,
                loan.LastInterestPaidDate,
                elapsedMonths,
                loan.LoanAmount,
                calc.TotalInterest,
                totalOutstanding,
                currentGoldVal,
                currentLtv,
                bucket,
                noticesSent,
                lastNoticeDate,
                isAuctionEligible
            ));
        }

        return list.OrderByDescending(x => x.TotalOutstanding).ToList();
    }

    public async Task<List<Notice>> GetNoticesForLoanAsync(Guid loanId, CancellationToken ct = default)
    {
        return await _context.Notices
            .Where(n => n.LoanId == loanId)
            .OrderByDescending(n => n.NoticeDate)
            .ToListAsync(ct);
    }

    public async Task<List<Notice>> IssueNoticeBatchAsync(CreateNoticeBatchDto dto, CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(dto.UserRole, Permission.CreateLoan);

        var notices = new List<Notice>();
        foreach (var loanId in dto.LoanIds)
        {
            var content = await GenerateNoticeHtmlOrTextAsync(loanId, dto.NoticeType, dto.NoticeDate, dto.DueDate, inTamil: true, ct);

            var notice = new Notice
            {
                LoanId = loanId,
                NoticeType = dto.NoticeType,
                NoticeDate = dto.NoticeDate,
                DueDate = dto.DueDate,
                Content = content,
                IsSent = true,
                SentAtUtc = DateTime.UtcNow,
                DeliveryMode = dto.DeliveryMode,
                Remarks = dto.Remarks ?? "Batch notice generated"
            };

            notices.Add(notice);
        }

        await _context.Notices.AddRangeAsync(notices, ct);
        await _context.SaveChangesAsync(ct);
        return notices;
    }

    public async Task<string> GenerateNoticeHtmlOrTextAsync(
        Guid loanId,
        NoticeType noticeType,
        DateOnly noticeDate,
        DateOnly dueDate,
        bool inTamil = true,
        CancellationToken ct = default)
    {
        var loan = await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .Include(l => l.Items).ThenInclude(i => i.JewelType)
            .FirstOrDefaultAsync(l => l.Id == loanId, ct)
            ?? throw new ArgumentException("Loan not found.");

        var calc = _calculator.CalculateInterest(
            loan.LoanAmount,
            loan.Scheme?.InterestRatePerMonth ?? 1.50m,
            loan.Scheme?.PenalInterestRatePerMonth ?? 0.50m,
            loan.PledgeDate,
            noticeDate,
            loan.LastInterestPaidDate,
            loan.Scheme?.MaxTenureMonths ?? 12,
            loan.Scheme?.GraceDaysAfterExpiry ?? 7
        );

        var sb = new StringBuilder();
        if (inTamil)
        {
            sb.AppendLine("================================================================================");
            sb.AppendLine("                      ✦ JGF JEWEL LOAN FINANCE ✦                               ");
            sb.AppendLine("                 அடகு கடன் நினைவூட்டல் மற்றும் ஏல அறிவிப்பு கடிதம்              ");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"தேதி / Date      : {noticeDate:dd-MMM-yyyy}");
            sb.AppendLine($"அறிவிப்பு வகை    : {noticeType}");
            sb.AppendLine($"கடன் எண் (GL No) : {loan.LoanNumber}");
            sb.AppendLine($"வாடிக்கையாளர்    : {loan.Customer.FullName} ({loan.Customer.NameTamil})");
            sb.AppendLine($"முகவரி          : {loan.Customer.Address}, {loan.Customer.City} - {loan.Customer.Pincode}");
            sb.AppendLine($"செல்போன் எண்     : {loan.Customer.MobileNumber}");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("வணக்கம்,");
            sb.AppendLine($"தாங்கள் எங்களது நிதி நிறுவனத்தில் {loan.PledgeDate:dd-MMM-yyyy} அன்று தங்களது தங்க நகைகளை");
            sb.AppendLine($"அடகு வைத்து ₹ {loan.LoanAmount:N2} கடன் பெற்றுள்ளீர்கள்.");
            sb.AppendLine();
            sb.AppendLine($"அசல் தொகை (Principal)        : ₹ {loan.LoanAmount,12:N2}");
            sb.AppendLine($"நிலுவை வட்டி (Accrued Int.)  : ₹ {calc.TotalInterest,12:N2}");
            sb.AppendLine($"மொத்த நிலுவை (Total Due)     : ₹ {calc.TotalOutstandingAmount,12:N2}");
            sb.AppendLine();
            sb.AppendLine("அடகு வைக்கப்பட்ட நகைகள் விவரம்:");
            int idx = 1;
            foreach (var item in loan.Items)
            {
                var jewelName = !string.IsNullOrWhiteSpace(item.JewelType?.NameTamil) ? item.JewelType.NameTamil : item.JewelType?.Name;
                sb.AppendLine($"  {idx++}. {jewelName} - {item.Description} (எண்ணிக்கை: {item.Quantity}, நிகர எடை: {item.NetWeight:F3}g)");
            }
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"மேற்படி கடன் தவணை முடிவடைந்துவிட்டதால், வரும் {dueDate:dd-MMM-yyyy} தேதிக்குள் அசலையும் வட்டியையும்");
            sb.AppendLine("செலுத்தி நகைகளை மீட்டுக்கொள்ளுமாறு கேட்டுக்கொள்கிறோம். தவறும் பட்சத்தில் சட்டப்படி");
            sb.AppendLine("தங்களது நகைகள் பொது ஏலத்தில் விடப்படும் என்பதை தெரிவித்துக்கொள்கிறோம்.");
            sb.AppendLine();
            sb.AppendLine("                                               இப்படிக்கு,");
            sb.AppendLine("                                           JGF JEWEL LOAN FINANCE");
            sb.AppendLine("================================================================================");
        }
        else
        {
            sb.AppendLine("================================================================================");
            sb.AppendLine("                      ✦ JGF JEWEL LOAN FINANCE ✦                               ");
            sb.AppendLine("                  DEMAND NOTICE & AUCTION INTIMATION                            ");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Notice Date : {noticeDate:dd-MMM-yyyy}");
            sb.AppendLine($"Notice Type : {noticeType}");
            sb.AppendLine($"Loan Number : {loan.LoanNumber}");
            sb.AppendLine($"Borrower    : {loan.Customer.FullName}");
            sb.AppendLine($"Address     : {loan.Customer.Address}, {loan.Customer.City} - {loan.Customer.Pincode}");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"This is to inform you that the gold loan availed by you on {loan.PledgeDate:dd-MMM-yyyy}");
            sb.AppendLine("has matured and is in overdue status.");
            sb.AppendLine($"Principal Balance : ₹ {loan.LoanAmount,12:N2}");
            sb.AppendLine($"Accrued Interest  : ₹ {calc.TotalInterest,12:N2}");
            sb.AppendLine($"Total Amount Due  : ₹ {calc.TotalOutstandingAmount,12:N2}");
            sb.AppendLine();
            sb.AppendLine($"Please settle the dues on or before {dueDate:dd-MMM-yyyy}, failing which the pledged");
            sb.AppendLine("gold ornaments will be auctioned in public auction under applicable laws.");
            sb.AppendLine("================================================================================");
        }

        return sb.ToString();
    }

    public async Task<Auction> ApproveAndScheduleAuctionAsync(
        Guid loanId,
        DateOnly auctionDate,
        decimal reservePrice,
        Guid ownerUserId,
        UserRole userRole,
        CancellationToken ct = default)
    {
        // Enforce Step 2: Owner approval ONLY
        if (userRole != UserRole.Owner)
        {
            throw new UnauthorizedAccessException("Auction approval is strictly restricted to the Owner.");
        }

        var loan = await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Notices)
            .FirstOrDefaultAsync(l => l.Id == loanId, ct)
            ?? throw new ArgumentException("Loan not found.");

        // Enforce Step 1: Minimum notice period served
        var hasNoticeServed = loan.Notices.Any(n =>
            (n.NoticeType == NoticeType.AuctionNotice || n.NoticeType == NoticeType.FinalNotice) &&
            n.NoticeDate.AddDays(15) <= auctionDate);

        if (!hasNoticeServed)
        {
            throw new InvalidOperationException(
                $"Cannot approve auction for Loan {loan.LoanNumber}. " +
                "A mandatory Final/Auction Notice must be served at least 15 days prior to the auction date under applicable rules.");
        }

        var existing = await _context.Auctions.FirstOrDefaultAsync(a => a.LoanId == loanId, ct);
        if (existing != null)
        {
            existing.AuctionDate = auctionDate;
            existing.ReservePrice = reservePrice;
            existing.Status = AuctionStatus.Scheduled;
            await _context.SaveChangesAsync(ct);
            return existing;
        }

        var auction = new Auction
        {
            LoanId = loanId,
            AuctionDate = auctionDate,
            ReservePrice = reservePrice,
            Status = AuctionStatus.Scheduled,
            Notes = $"Auction approved and scheduled by Owner on {DateTime.UtcNow:dd-MMM-yyyy}"
        };

        await _context.Auctions.AddAsync(auction, ct);
        await _context.SaveChangesAsync(ct);
        return auction;
    }

    public async Task<AuctionSettlementResultDto> ConductAndSettleAuctionAsync(ConductAuctionDto dto, CancellationToken ct = default)
    {
        // Auction execution is restricted to Owner / Manager
        if (dto.UserRole != UserRole.Owner && dto.UserRole != UserRole.Manager)
        {
            throw new UnauthorizedAccessException("Only Owner or Manager can conduct auction settlement.");
        }

        var loan = await _context.Loans
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .Include(l => l.Auctions)
            .FirstOrDefaultAsync(l => l.Id == dto.LoanId, ct)
            ?? throw new ArgumentException("Loan not found.");

        var auction = loan.Auctions.FirstOrDefault(a => a.Status == AuctionStatus.Scheduled)
            ?? throw new InvalidOperationException("No scheduled auction found for this loan. Owner must approve auction first.");

        var isRelational = _context.Database.IsRelational();
        using var tx = isRelational ? await _context.Database.BeginTransactionAsync(ct) : null;

        // Calculate total outstanding on auction date
        var calc = _calculator.CalculateInterest(
            loan.LoanAmount,
            loan.Scheme?.InterestRatePerMonth ?? 1.50m,
            loan.Scheme?.PenalInterestRatePerMonth ?? 0.50m,
            loan.PledgeDate,
            dto.AuctionDate,
            loan.LastInterestPaidDate,
            loan.Scheme?.MaxTenureMonths ?? 12,
            loan.Scheme?.GraceDaysAfterExpiry ?? 7
        );

        var totalDues = loan.LoanAmount + calc.TotalInterest + dto.AuctionExpenses;
        decimal surplus = 0m;
        decimal deficit = 0m;

        if (dto.SalePrice >= totalDues)
        {
            surplus = dto.SalePrice - totalDues; // Payable to customer
        }
        else
        {
            deficit = totalDues - dto.SalePrice; // Loss / Shortfall
        }

        // Update Auction record
        auction.SalePrice = dto.SalePrice;
        auction.BuyerName = dto.BuyerName;
        auction.BuyerAddress = dto.BuyerAddress;
        auction.OutstandingPrincipal = loan.LoanAmount;
        auction.OutstandingInterest = calc.TotalInterest;
        auction.AuctionCharges = dto.AuctionExpenses;
        auction.SurplusAmount = surplus;
        auction.DeficitAmount = deficit;
        auction.Status = AuctionStatus.Conducted;
        auction.Notes = dto.Remarks;

        // Mark loan Auctioned & Closed
        loan.Status = LoanStatus.Auctioned;
        loan.ClosedDate = dto.AuctionDate;

        // Post balanced double-entry voucher
        var fy = "2024-25";
        var vNumber = await _numberSeries.GetNextNumberAsync(NumberSeriesType.VoucherNumber, fy, null, ct);

        var headCash = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1001", ct);
        var headJewelLoan = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "1002", ct);
        var headInterest = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "4001", ct);
        var headSurplus = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "2002", ct); // Surplus Payable
        var headDeficit = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "5005", ct); // Loss on Auction

        var voucher = new Voucher
        {
            VoucherNumber = vNumber,
            VoucherDate = dto.AuctionDate,
            VoucherType = VoucherType.Journal,
            TotalDebit = 0m,
            TotalCredit = 0m,
            Narration = $"Auction settlement for Loan #{loan.LoanNumber} ({loan.Customer.FullName})",
            IsPosted = true,
            PostedAtUtc = DateTime.UtcNow,
            PostedByUserId = dto.UserId,
            ReferenceType = "Auction",
            ReferenceId = auction.Id
        };

        int line = 1;
        // Dr Cash (Sale Proceeds received from Buyer)
        voucher.Lines.Add(new VoucherLine
        {
            LedgerHeadId = headCash!.Id,
            DebitAmount = dto.SalePrice,
            CreditAmount = 0m,
            Narration = $"Auction sale proceeds from {dto.BuyerName}",
            LineOrder = line++
        });

        // If Deficit: Dr Loss on Auction (5005)
        if (deficit > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headDeficit!.Id,
                DebitAmount = deficit,
                CreditAmount = 0m,
                Narration = $"Auction shortfall/deficit on {loan.LoanNumber}",
                LineOrder = line++
            });
        }

        // Cr Jewel Loan Receivable (Zero out principal)
        voucher.Lines.Add(new VoucherLine
        {
            LedgerHeadId = headJewelLoan!.Id,
            DebitAmount = 0m,
            CreditAmount = loan.LoanAmount,
            Narration = $"Principal settled via auction {loan.LoanNumber}",
            LineOrder = line++
        });

        // Cr Interest Income
        if (calc.TotalInterest > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headInterest!.Id,
                DebitAmount = 0m,
                CreditAmount = calc.TotalInterest,
                Narration = $"Interest recovered via auction {loan.LoanNumber}",
                LineOrder = line++
            });
        }

        // Cr Auction Charges / Expense Reimbursement
        if (dto.AuctionExpenses > 0)
        {
            var headExpenses = await _context.LedgerHeads.FirstOrDefaultAsync(l => l.Code == "4003" || l.Code == "5005", ct) ?? headCash;
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headExpenses!.Id,
                DebitAmount = 0m,
                CreditAmount = dto.AuctionExpenses,
                Narration = $"Auction charges recovered on {loan.LoanNumber}",
                LineOrder = line++
            });
        }

        // If Surplus: Cr Auction Surplus Payable (2002)
        if (surplus > 0)
        {
            voucher.Lines.Add(new VoucherLine
            {
                LedgerHeadId = headSurplus!.Id,
                DebitAmount = 0m,
                CreditAmount = surplus,
                Narration = $"Auction surplus payable to customer {loan.Customer.FullName}",
                LineOrder = line++
            });
        }

        // Calculate balanced totals
        var totalDr = voucher.Lines.Sum(l => l.DebitAmount);
        var totalCr = voucher.Lines.Sum(l => l.CreditAmount);
        voucher.TotalDebit = totalDr;
        voucher.TotalCredit = totalCr;

        await _context.Vouchers.AddAsync(voucher, ct);
        auction.VoucherId = voucher.Id;

        // Sub-ledger closure
        await _context.LoanLedgerEntries.AddAsync(new LoanLedgerEntry
        {
            LoanId = loan.Id,
            EntryDate = dto.AuctionDate,
            EntryType = LoanLedgerEntryType.Closure,
            DebitAmount = 0m,
            CreditAmount = loan.LoanAmount,
            Balance = 0m,
            Narration = $"Loan liquidated in auction. Sale: ₹{dto.SalePrice:N2}, Surplus: ₹{surplus:N2}, Deficit: ₹{deficit:N2}"
        }, ct);

        await _context.SaveChangesAsync(ct);
        if (tx != null)
        {
            await tx.CommitAsync(ct);
        }

        return new AuctionSettlementResultDto(
            auction.Id,
            loan.LoanNumber,
            dto.SalePrice,
            totalDues,
            dto.AuctionExpenses,
            surplus,
            deficit,
            vNumber
        );
    }

    public async Task<List<Auction>> GetAuctionRegisterAsync(CancellationToken ct = default)
    {
        return await _context.Auctions
            .Include(a => a.Loan).ThenInclude(l => l.Customer)
            .Include(a => a.Loan).ThenInclude(l => l.Items)
            .OrderByDescending(a => a.AuctionDate)
            .ToListAsync(ct);
    }
}

