using FluentAssertions;
using JGF.Application.Loans;
using JGF.Application.Notices;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JGF.Tests.Notices;

public class NoticeAndAuctionTests
{
    private static JgfDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: $"JgfNoticeAuctionTestDb_{Guid.NewGuid():N}")
            .Options;

        var context = new JgfDbContext(options);

        // Seed Chart of Accounts
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", OpeningBalance = 500000m },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit" },
            new LedgerHead { Code = "2002", Name = "Auction Surplus Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit" },
            new LedgerHead { Code = "4001", Name = "Jewel Loan Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit" },
            new LedgerHead { Code = "5005", Name = "Loss on Auction Deficit", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit" }
        );

        // Seed Daily Gold Rate
        context.GoldRates.Add(new GoldRate
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Rate22K = 5500.00m,
            Rate24K = 6000.00m
        });

        // Seed Scheme
        var scheme = new Scheme
        {
            Code = "SCH01",
            Name = "Standard Jewel Loan Scheme",
            InterestRatePerMonth = 1.50m,
            PenalInterestRatePerMonth = 0.50m,
            MaxTenureMonths = 12,
            GraceDaysAfterExpiry = 7,
            MinimumInterestDays = 30,
            MaxLtvPercentage = 75.0m,
            IsActive = true
        };
        context.Schemes.Add(scheme);

        // Seed Customers
        var customer1 = new Customer
        {
            Code = "C001",
            FullName = "Sundaram K",
            NameTamil = "சுந்தரம் கே",
            MobileNumber = "9443322110",
            Address = "12 North Car Street",
            City = "Madurai",
            Pincode = "625001"
        };
        var customer2 = new Customer
        {
            Code = "C002",
            FullName = "Murugesan P",
            NameTamil = "முருகேசன் பி",
            MobileNumber = "9842112233",
            Address = "45 West Masi St",
            City = "Madurai",
            Pincode = "625001"
        };
        context.Customers.AddRange(customer1, customer2);

        // Seed Jewel Types
        var jewelType = new JewelType { Code = "JT01", Name = "Gold Chain", NameTamil = "தங்க செயின்", Purity = 22.0m, IsActive = true };
        context.JewelTypes.Add(jewelType);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task OverdueClassification_ShouldAgeLoansThroughThresholds_Correctly()
    {
        using var context = CreateInMemoryContext();
        var scheme = await context.Schemes.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 1. Normal Loan (2 months old)
        var loanNormal = new Loan
        {
            LoanNumber = "GL-NORM-01",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today.AddDays(-60),
            MaturityDate = today.AddDays(300),
            LoanAmount = 40000m,
            MarketValue = 60000m,
            NetWeight = 12.0m,
            GrossWeight = 13.0m,
            Status = LoanStatus.Active
        };

        // 2. 6-Month Reminder Loan (7 months old, 210 days)
        var loan6M = new Loan
        {
            LoanNumber = "GL-6M-02",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today.AddDays(-210),
            MaturityDate = today.AddDays(150),
            LoanAmount = 50000m,
            MarketValue = 75000m,
            NetWeight = 15.0m,
            GrossWeight = 16.0m,
            Status = LoanStatus.Active
        };

        // 3. 12-Month Matured Loan (13 months old, 395 days)
        var loan12M = new Loan
        {
            LoanNumber = "GL-12M-03",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today.AddDays(-395),
            MaturityDate = today.AddDays(-30),
            LoanAmount = 60000m,
            MarketValue = 90000m,
            NetWeight = 18.0m,
            GrossWeight = 19.0m,
            Status = LoanStatus.Active
        };

        context.Loans.AddRange(loanNormal, loan6M, loan12M);
        await context.SaveChangesAsync();

        var calc = new StandardInterestCalculator();
        var masterData = new MasterDataService(context);
        var perm = new PermissionService();
        var service = new OverdueNoticeService(context, calc, masterData, perm);

        var overdues = await service.GetOverdueLoansAsync();

        var resNormal = overdues.First(l => l.LoanNumber == "GL-NORM-01");
        resNormal.Bucket.Should().Be(OverdueBucket.Normal);

        var res6M = overdues.First(l => l.LoanNumber == "GL-6M-02");
        res6M.Bucket.Should().Be(OverdueBucket.SixMonthReminder);
        res6M.ElapsedMonths.Should().BeGreaterThanOrEqualTo(6);

        var res12M = overdues.First(l => l.LoanNumber == "GL-12M-03");
        res12M.Bucket.Should().Be(OverdueBucket.TwelveMonthMatured);
        res12M.ElapsedMonths.Should().BeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public async Task NoticeGeneration_ShouldProduceAccurateBilingualNoticeLetters()
    {
        using var context = CreateInMemoryContext();
        var scheme = await context.Schemes.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var jewelType = await context.JewelTypes.FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loan = new Loan
        {
            LoanNumber = "GL-NOTC-100",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today.AddDays(-200),
            MaturityDate = today.AddDays(165),
            LoanAmount = 45000m,
            MarketValue = 65000m,
            NetWeight = 13.0m,
            GrossWeight = 14.0m,
            Status = LoanStatus.Active
        };
        loan.Items.Add(new LoanItem
        {
            JewelTypeId = jewelType.Id,
            JewelType = jewelType,
            Description = "916 Gold Rope Chain",
            Quantity = 1,
            GrossWeight = 14.0m,
            NetWeight = 13.0m,
            Purity = 22.0m,
            RatePerGram = 5000m,
            TotalValue = 65000m
        });

        context.Loans.Add(loan);
        await context.SaveChangesAsync();

        var calc = new StandardInterestCalculator();
        var masterData = new MasterDataService(context);
        var perm = new PermissionService();
        var service = new OverdueNoticeService(context, calc, masterData, perm);

        // Test Tamil Notice Generation
        var tamilLetter = await service.GenerateNoticeHtmlOrTextAsync(
            loan.Id,
            NoticeType.FinalNotice,
            today,
            today.AddDays(15),
            inTamil: true
        );

        tamilLetter.Should().Contain("GL-NOTC-100");
        tamilLetter.Should().Contain("சுந்தரம்");
        tamilLetter.Should().Contain("அசல் தொகை");
        tamilLetter.Should().Contain("பொது ஏலத்தில் விடப்படும்");
        tamilLetter.Should().Contain("தங்க செயின்");

        // Test English Notice Generation
        var englishLetter = await service.GenerateNoticeHtmlOrTextAsync(
            loan.Id,
            NoticeType.AuctionNotice,
            today,
            today.AddDays(15),
            inTamil: false
        );

        englishLetter.Should().Contain("GL-NOTC-100");
        englishLetter.Should().Contain("DEMAND NOTICE");
        englishLetter.Should().Contain("Sundaram K");
        englishLetter.Should().Contain("Principal Balance");
        englishLetter.Should().Contain("auctioned in public auction");
    }

    [Fact]
    public async Task AuctionScheduling_ShouldEnforceStatutoryNoticePeriod_AndOwnerApprovalGate()
    {
        using var context = CreateInMemoryContext();
        var scheme = await context.Schemes.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loan = new Loan
        {
            LoanNumber = "GL-AUC-TEST-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today.AddDays(-400),
            MaturityDate = today.AddDays(-35),
            LoanAmount = 50000m,
            MarketValue = 75000m,
            NetWeight = 15.0m,
            GrossWeight = 16.0m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);
        await context.SaveChangesAsync();

        var calc = new StandardInterestCalculator();
        var masterData = new MasterDataService(context);
        var perm = new PermissionService();
        var service = new OverdueNoticeService(context, calc, masterData, perm);

        // Case 1: Non-Owner approval attempt should throw UnauthorizedAccessException
        Func<Task> actNonOwner = async () =>
        {
            await service.ApproveAndScheduleAuctionAsync(
                loan.Id,
                today.AddDays(7),
                55000m,
                Guid.NewGuid(),
                UserRole.Cashier
            );
        };
        await actNonOwner.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Owner*");

        // Case 2: Owner approval without serving pre-auction notice should throw InvalidOperationException
        Func<Task> actNoNotice = async () =>
        {
            await service.ApproveAndScheduleAuctionAsync(
                loan.Id,
                today.AddDays(7),
                55000m,
                Guid.NewGuid(),
                UserRole.Owner
            );
        };
        await actNoNotice.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*mandatory Final/Auction Notice must be served*");

        // Case 3: Serve Pre-Auction Notice, but notice served less than 15 days ago
        context.Notices.Add(new Notice
        {
            LoanId = loan.Id,
            NoticeType = NoticeType.AuctionNotice,
            NoticeDate = today.AddDays(-5), // Only 5 days ago!
            DueDate = today.AddDays(10),
            SentVia = NoticeSentVia.RegisteredPost,
            Remarks = "Statutory 15-day notice"
        });
        await context.SaveChangesAsync();

        Func<Task> actNoticeNotMatured = async () =>
        {
            await service.ApproveAndScheduleAuctionAsync(
                loan.Id,
                today.AddDays(7),
                55000m,
                Guid.NewGuid(),
                UserRole.Owner
            );
        };
        await actNoticeNotMatured.Should().ThrowAsync<InvalidOperationException>();

        // Case 4: Notice served 20 days ago (Statutory requirement met) -> Auction successfully scheduled
        var servedNotice = await context.Notices.FirstAsync();
        servedNotice.NoticeDate = today.AddDays(-20);
        await context.SaveChangesAsync();

        var auction = await service.ApproveAndScheduleAuctionAsync(
            loan.Id,
            today.AddDays(7),
            55000m,
            Guid.NewGuid(),
            UserRole.Owner
        );

        auction.Should().NotBeNull();
        auction.Status.Should().Be(AuctionStatus.Scheduled);
        auction.ReservePrice.Should().Be(55000m);
    }

    [Fact]
    public async Task ConductAuctionSettlement_ShouldPostBalancedDoubleEntryVouchers_ForSurplusCase()
    {
        using var context = CreateInMemoryContext();
        var scheme = await context.Schemes.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loan = new Loan
        {
            LoanNumber = "GL-AUC-SURPLUS-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today.AddDays(-380),
            MaturityDate = today.AddDays(-15),
            LoanAmount = 40000m,
            MarketValue = 70000m,
            NetWeight = 14.0m,
            GrossWeight = 15.0m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);

        // Pre-notice served 25 days ago
        context.Notices.Add(new Notice
        {
            LoanId = loan.Id,
            NoticeType = NoticeType.AuctionNotice,
            NoticeDate = today.AddDays(-25),
            DueDate = today.AddDays(-10),
            SentVia = NoticeSentVia.RegisteredPost
        });
        await context.SaveChangesAsync();

        var calc = new StandardInterestCalculator();
        var masterData = new MasterDataService(context);
        var perm = new PermissionService();
        var service = new OverdueNoticeService(context, calc, masterData, perm);

        // Owner approves auction
        await service.ApproveAndScheduleAuctionAsync(loan.Id, today, 45000m, Guid.NewGuid(), UserRole.Owner);

        // Gold sells for ₹65,000 at auction. Total dues are ~₹47,200 + ₹1,000 expenses.
        // There is a Surplus payable to customer!
        var conductDto = new ConductAuctionDto(
            loan.Id,
            today,
            "Madurai Jewel Hall",
            45000m,
            65000m, // Winning Bid
            1000m,  // Expenses
            "Thangavel Jewellers",
            "77 South Avani Moola St",
            "Public auction surplus settlement",
            Guid.NewGuid(),
            UserRole.Owner
        );

        var result = await service.ConductAndSettleAuctionAsync(conductDto);

        result.Should().NotBeNull();
        result.SalePrice.Should().Be(65000m);
        result.SurplusAmount.Should().BeGreaterThan(0);
        result.DeficitAmount.Should().Be(0m);

        // Check Loan Status
        var updatedLoan = await context.Loans.FindAsync(loan.Id);
        updatedLoan!.Status.Should().Be(LoanStatus.Auctioned);
        updatedLoan.ClosedDate.Should().Be(today);

        // Verify Double Entry Voucher in DB
        var voucher = await context.Vouchers
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.VoucherNumber == result.VoucherNumber);

        voucher.Should().NotBeNull();
        var totalDebits = voucher!.Entries.Sum(e => e.DebitAmount);
        var totalCredits = voucher.Entries.Sum(e => e.CreditAmount);

        // MANDATORY DOUBLE-ENTRY FINANCIAL CONSTRAINT:
        totalDebits.Should().Be(totalCredits, "Double entry debits must exactly balance credits");
        totalDebits.Should().Be(65000m, "Debit Cash must equal total auction proceeds");

        // Credit should contain: Jewel Loan (40,000) + Accrued Interest + Surplus Payable
        voucher.Entries.Any(e => e.CreditAmount == 40000m).Should().BeTrue("Jewel loan principal must be credited");
        voucher.Entries.Any(e => e.CreditAmount == result.SurplusAmount).Should().BeTrue("Surplus payable to customer must be credited");
    }

    [Fact]
    public async Task ConductAuctionSettlement_ShouldPostBalancedDoubleEntryVouchers_ForDeficitCase()
    {
        using var context = CreateInMemoryContext();
        var scheme = await context.Schemes.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loan = new Loan
        {
            LoanNumber = "GL-AUC-DEFICIT-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today.AddDays(-400),
            MaturityDate = today.AddDays(-35),
            LoanAmount = 50000m,
            MarketValue = 45000m, // Damaged/low purity gold, value dropped
            NetWeight = 10.0m,
            GrossWeight = 11.0m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);

        // Pre-notice served 25 days ago
        context.Notices.Add(new Notice
        {
            LoanId = loan.Id,
            NoticeType = NoticeType.AuctionNotice,
            NoticeDate = today.AddDays(-25),
            DueDate = today.AddDays(-10),
            SentVia = NoticeSentVia.RegisteredPost
        });
        await context.SaveChangesAsync();

        var calc = new StandardInterestCalculator();
        var masterData = new MasterDataService(context);
        var perm = new PermissionService();
        var service = new OverdueNoticeService(context, calc, masterData, perm);

        // Owner approves auction
        await service.ApproveAndScheduleAuctionAsync(loan.Id, today, 40000m, Guid.NewGuid(), UserRole.Owner);

        // Gold sells for ₹42,000 at auction. Total dues are ~₹59,000 + ₹500 expenses.
        // There is a DEFICIT / LOSS booked to bad debts!
        var conductDto = new ConductAuctionDto(
            loan.Id,
            today,
            "Madurai Jewel Hall",
            40000m,
            42000m, // Winning Bid
            500m,   // Expenses
            "Selvam Traders",
            "14 Market Road",
            "Auction shortfall deficit booked as loss",
            Guid.NewGuid(),
            UserRole.Owner
        );

        var result = await service.ConductAndSettleAuctionAsync(conductDto);

        result.Should().NotBeNull();
        result.SalePrice.Should().Be(42000m);
        result.SurplusAmount.Should().Be(0m);
        result.DeficitAmount.Should().BeGreaterThan(0);

        // Verify Double Entry Voucher in DB
        var voucher = await context.Vouchers
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.VoucherNumber == result.VoucherNumber);

        voucher.Should().NotBeNull();
        var totalDebits = voucher!.Entries.Sum(e => e.DebitAmount);
        var totalCredits = voucher.Entries.Sum(e => e.CreditAmount);

        // MANDATORY DOUBLE-ENTRY FINANCIAL CONSTRAINT:
        totalDebits.Should().Be(totalCredits, "Double entry debits must balance credits in deficit scenario");

        // Debits include: Cash (42,000) + Loss on Auction Deficit
        voucher.Entries.Any(e => e.DebitAmount == 42000m).Should().BeTrue("Debit Cash must equal sale price");
        voucher.Entries.Any(e => e.DebitAmount == result.DeficitAmount).Should().BeTrue("Debit Loss on Auction must equal deficit");
    }
}

