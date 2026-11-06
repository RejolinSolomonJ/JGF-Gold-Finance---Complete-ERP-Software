using FluentAssertions;
using JGF.Application.Dashboard;
using JGF.Application.Loans;
using JGF.Application.Reports;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JGF.Tests.Reports;

public class JlReportAndDashboardTests
{
    private static JgfDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: $"JgfReportTestDb_{Guid.NewGuid():N}")
            .Options;

        var context = new JgfDbContext(options);

        // Standard Chart of Accounts
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", OpeningBalance = 1000000m },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit" },
            new LedgerHead { Code = "2001", Name = "Bank Repledge Loan Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit" },
            new LedgerHead { Code = "3001", Name = "Owner's Capital", AccountGroup = LedgerHeadType.Capital, NormalBalance = "Credit", OpeningBalance = 1000000m },
            new LedgerHead { Code = "4001", Name = "Jewel Loan Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit" },
            new LedgerHead { Code = "4002", Name = "Penal Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit" },
            new LedgerHead { Code = "4004", Name = "Postal Charges Recovered", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit" },
            new LedgerHead { Code = "5001", Name = "Bank Repledge Interest Expense", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit" }
        );

        // Jewel Types
        context.JewelTypes.AddRange(
            new JewelType { Code = "CH", Name = "Gold Chain", Description = "Chain", IsActive = true },
            new JewelType { Code = "RG", Name = "Gold Ring", Description = "Ring", IsActive = true },
            new JewelType { Code = "BG", Name = "Gold Bangle", Description = "Bangle", IsActive = true }
        );

        // Number Series
        context.NumberSerieses.AddRange(
            new NumberSeries { SeriesType = NumberSeriesType.CustomerCode, Prefix = "C", FinancialYear = "2024-25", LastNumber = 100, PadLength = 4, Separator = "" },
            new NumberSeries { SeriesType = NumberSeriesType.LoanNumber, Prefix = "GL", FinancialYear = "2024-25", LastNumber = 100, PadLength = 5, Separator = "/" },
            new NumberSeries { SeriesType = NumberSeriesType.ReceiptNumber, Prefix = "R", FinancialYear = "2024-25", LastNumber = 100, PadLength = 5, Separator = "/" },
            new NumberSeries { SeriesType = NumberSeriesType.VoucherNumber, Prefix = "V", FinancialYear = "2024-25", LastNumber = 100, PadLength = 5, Separator = "/" }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task OutstandingAndSummaryReports_ShouldAccuratelyCalculateBalancesAndTieToData()
    {
        using var context = CreateInMemoryContext();
        var masterDataService = new MasterDataService(context);
        var permissionService = new PermissionService();
        var interestCalculator = new StandardInterestCalculator();
        var reportService = new JlReportService(context, masterDataService, permissionService, interestCalculator);

        var scheme = new Scheme
        {
            Code = "SCH-A",
            Name = "Regular 1.5%",
            InterestRatePerMonth = 1.50m,
            PenalInterestRatePerMonth = 0.50m,
            MaxTenureMonths = 12,
            MinimumInterestDays = 30,
            GracePeriodDays = 7,
            MaxLtvPercent = 75m,
            IsActive = true
        };
        context.Schemes.Add(scheme);

        var customer = new Customer
        {
            Code = "C0001",
            Name = "Murugan K",
            MobileNumber = "9876543210"
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var pledgeDate = today.AddDays(-60);

        var loan = new Loan
        {
            LoanNumber = "GL/24-25/00001",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = pledgeDate,
            MaturityDate = pledgeDate.AddYears(1),
            LoanAmount = 50000m,
            GrossWeight = 20.000m,
            NetWeight = 19.500m,
            Status = LoanStatus.Active,
            PacketNumber = "PKT-101"
        };
        context.Loans.Add(loan);

        // Customer made a part principal payment of ₹10,000 + interest ₹1,500 10 days ago
        var receipt = new Receipt
        {
            ReceiptNumber = "R/24-25/00001",
            LoanId = loan.Id,
            ReceiptDate = today.AddDays(-10),
            ReceiptType = ReceiptType.PartPrincipal,
            PrincipalAmount = 10000m,
            InterestAmount = 1500m,
            TotalAmount = 11500m,
            PaymentMode = PaymentMode.Cash
        };
        context.Receipts.Add(receipt);
        loan.LastInterestPaidDate = today.AddDays(-10);

        await context.SaveChangesAsync();

        // 1. Test Outstanding Report
        var outstanding = await reportService.GetOutstandingReportAsync(new JlReportFilterDto());
        outstanding.TotalLoans.Should().Be(1);
        outstanding.TotalDisbursedPrincipal.Should().Be(50000m);
        outstanding.TotalOutstandingPrincipal.Should().Be(40000m); // 50,000 - 10,000 repaid
        outstanding.TotalNetWeight.Should().Be(19.500m);
        outstanding.Items[0].StorageLocation.Should().Be("Shop Locker");
        outstanding.Items[0].IsOverdue.Should().BeFalse();

        // 2. Test Scheme Summary Report
        var summary = await reportService.GetLoanSummaryReportAsync(new JlReportFilterDto());
        summary.GrandTotalLoans.Should().Be(1);
        summary.GrandTotalActive.Should().Be(1);
        summary.GrandDisbursed.Should().Be(50000m);
        summary.GrandPrincipalCollected.Should().Be(10000m);
        summary.GrandOutstandingPrincipal.Should().Be(40000m);
        summary.GrandInterestCollected.Should().Be(1500m);
    }

    [Fact]
    public async Task CustomerStatement_ShouldProvideChronologicalRunningPrincipal()
    {
        using var context = CreateInMemoryContext();
        var masterDataService = new MasterDataService(context);
        var permissionService = new PermissionService();
        var interestCalculator = new StandardInterestCalculator();
        var reportService = new JlReportService(context, masterDataService, permissionService, interestCalculator);

        var scheme = new Scheme { Code = "SCH-B", Name = "Quick Loan", InterestRatePerMonth = 2.0m, MaxTenureMonths = 12 };
        var customer = new Customer { Code = "C0002", Name = "Kavitha S", MobileNumber = "9840123456" };
        context.Schemes.Add(scheme);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var dt1 = new DateOnly(2026, 1, 10);
        var dt2 = new DateOnly(2026, 3, 10);
        var dt3 = new DateOnly(2026, 5, 10);

        var loan = new Loan
        {
            LoanNumber = "GL/24-25/00002",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = dt1,
            MaturityDate = dt1.AddYears(1),
            LoanAmount = 100000m,
            GrossWeight = 30m,
            NetWeight = 28.5m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);

        // First part payment
        context.Receipts.Add(new Receipt
        {
            ReceiptNumber = "R/24-25/00002",
            LoanId = loan.Id,
            ReceiptDate = dt2,
            ReceiptType = ReceiptType.PartPrincipal,
            PrincipalAmount = 30000m,
            InterestAmount = 4000m,
            TotalAmount = 34000m
        });

        // Second part payment
        context.Receipts.Add(new Receipt
        {
            ReceiptNumber = "R/24-25/00003",
            LoanId = loan.Id,
            ReceiptDate = dt3,
            ReceiptType = ReceiptType.PartPrincipal,
            PrincipalAmount = 20000m,
            InterestAmount = 2800m,
            TotalAmount = 22800m
        });

        await context.SaveChangesAsync();

        var statement = await reportService.GetCustomerStatementAsync(customer.Id);
        statement.TotalLoansTaken.Should().Be(1);
        statement.ActiveLoansCount.Should().Be(1);
        statement.CurrentOutstandingPrincipal.Should().Be(50000m); // 100,000 - 30,000 - 20,000

        statement.Transactions.Should().HaveCount(3);
        statement.Transactions[0].RunningPrincipalBalance.Should().Be(100000m);
        statement.Transactions[1].RunningPrincipalBalance.Should().Be(70000m);
        statement.Transactions[2].RunningPrincipalBalance.Should().Be(50000m);
    }

    [Fact]
    public async Task PostalChargeRecording_ShouldPostBalancedVoucherAndReflectInReport()
    {
        using var context = CreateInMemoryContext();
        var masterDataService = new MasterDataService(context);
        var permissionService = new PermissionService();
        var interestCalculator = new StandardInterestCalculator();
        var reportService = new JlReportService(context, masterDataService, permissionService, interestCalculator);

        var scheme = new Scheme { Code = "SCH-C", Name = "Standard", MaxTenureMonths = 12 };
        var customer = new Customer { Code = "C0003", Name = "Ramesh P", MobileNumber = "9988776655" };
        context.Schemes.Add(scheme);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "GL/24-25/00003",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-6)),
            MaturityDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
            LoanAmount = 45000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);
        await context.SaveChangesAsync();

        var postalDto = new RecordPostalChargeDto(
            loan.Id,
            Amount: 120m,
            ChargeDate: DateOnly.FromDateTime(DateTime.Today),
            Narration: "Registered auction reminder notice postage",
            PaymentMode: PaymentMode.Cash,
            UserId: Guid.NewGuid(),
            UserRole: UserRole.Owner
        );

        var vNumber = await reportService.RecordPostalChargeAsync(postalDto);
        vNumber.Should().NotBeNullOrWhiteSpace();

        // Verify balanced voucher
        var voucher = await context.Vouchers.Include(v => v.Lines).FirstOrDefaultAsync(v => v.VoucherNumber == vNumber);
        voucher.Should().NotBeNull();
        voucher!.TotalDebit.Should().Be(120m);
        voucher.TotalCredit.Should().Be(120m);
        voucher.Lines.Sum(l => l.DebitAmount).Should().Be(120m);
        voucher.Lines.Sum(l => l.CreditAmount).Should().Be(120m);

        // Verify report
        var postalReport = await reportService.GetPostalChargeReportAsync(new JlReportFilterDto());
        postalReport.TotalEntries.Should().Be(1);
        postalReport.TotalPostalAmount.Should().Be(120m);
        postalReport.Items[0].GlNumber.Should().Be("GL/24-25/00003");
        postalReport.Items[0].CustomerName.Should().Be("Ramesh P");
    }

    [Fact]
    public async Task ExcelExportService_ShouldGenerateValidXlsxWorkbook()
    {
        var exportService = new ExcelExportService();

        var cols = new List<ExcelColumnDefinition>
        {
            new("GL Number", 12),
            new("Customer", 20),
            new("Principal (₹)", 16, "₹ #,##0.00", true)
        };

        var rows = new List<List<object?>>
        {
            new() { "GL/001", "Murugan", 50000m },
            new() { "GL/002", "Kavitha", 75000m }
        };

        var totals = new List<object?> { "TOTAL", null, 125000m };

        var bytes = exportService.ExportToExcel("Test Jewel Loan Report", "Sample Subtitle", cols, rows, totals);

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(1000); // Valid zip/xlsx file format
    }

    [Fact]
    public async Task DashboardService_ShouldReturnCompleteKpisAndReflectCustodySplit()
    {
        using var context = CreateInMemoryContext();
        var backupService = new BackupService(context, new AesEncryptionService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupService>.Instance);
        var dashboardService = new DashboardService(context, backupService);

        var today = DateOnly.FromDateTime(DateTime.Today);

        var scheme = new Scheme { Code = "S1", Name = "Standard", MaxTenureMonths = 12 };
        var customer = new Customer { Code = "C1", Name = "Sundar", MobileNumber = "9111111111" };
        var bank = new Bank { Code = "SBI", Name = "State Bank of India", SanctionedLimit = 5000000m };

        context.Schemes.Add(scheme);
        context.Customers.Add(customer);
        context.Banks.Add(bank);
        await context.SaveChangesAsync();

        // 1 Loan held in Shop Locker (25.000g, 60,000)
        var loanInLocker = new Loan
        {
            LoanNumber = "GL-LOCKER-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today,
            MaturityDate = today.AddYears(1),
            LoanAmount = 60000m,
            NetWeight = 25.000m,
            GrossWeight = 26.000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loanInLocker);

        // 1 Loan pledged to Bank (35.000g, 80,000)
        var loanInBank = new Loan
        {
            LoanNumber = "GL-BANK-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = today,
            MaturityDate = today.AddYears(1),
            LoanAmount = 80000m,
            NetWeight = 35.000m,
            GrossWeight = 36.500m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loanInBank);

        var bankPledge = new BankPledge
        {
            BankId = bank.Id,
            PledgeNumber = "BP/001",
            PledgeDate = today,
            DisbursedAmount = 70000m,
            Status = BankPledgeStatus.Active,
            MaturityDate = today.AddDays(15)
        };
        context.BankPledges.Add(bankPledge);

        context.BankPledgeLoans.Add(new BankPledgeLoan
        {
            BankPledge = bankPledge,
            Loan = loanInBank,
            PledgedAmount = 70000m,
            PledgedDate = today,
            IsReleased = false
        });

        await context.SaveChangesAsync();

        var summary = await dashboardService.GetDashboardSummaryAsync();

        summary.LoansIssuedTodayCount.Should().Be(2);
        summary.LoansIssuedTodayAmount.Should().Be(140000m);
        summary.TotalActiveLoansCount.Should().Be(2);
        summary.TotalOutstandingPrincipal.Should().Be(140000m);

        // Gold custody split
        summary.TotalGoldWeightShopLocker.Should().Be(25.000m);
        summary.TotalGoldWeightBankCustody.Should().Be(35.000m);
        summary.TotalGoldWeightOverall.Should().Be(60.000m);

        // Bank exposure & upcoming due within 30 days
        summary.BankRepledgeTotalExposure.Should().Be(70000m);
        summary.BankRepledgeActiveCount.Should().Be(1);
        summary.BankUpcomingDues.Should().HaveCount(1);
        summary.BankUpcomingDues[0].BankName.Should().Be("State Bank of India");
        summary.BankUpcomingDues[0].DaysRemaining.Should().Be(15);
    }

    [Fact]
    public async Task PerformanceAndIntegrityReconciliation_ShouldLoadFastAndTieWithZeroDiscrepancy()
    {
        using var context = CreateInMemoryContext();
        var masterDataService = new MasterDataService(context);
        var permissionService = new PermissionService();
        var interestCalculator = new StandardInterestCalculator();
        var reportService = new JlReportService(context, masterDataService, permissionService, interestCalculator);
        var backupService = new BackupService(context, new AesEncryptionService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupService>.Instance);
        var dashboardService = new DashboardService(context, backupService);
        var accountingService = new AccountingReportService(context, masterDataService, permissionService);

        var scheme = new Scheme { Code = "S1", Name = "Standard Scheme", InterestRatePerMonth = 1.5m, MaxTenureMonths = 12 };
        context.Schemes.Add(scheme);

        var cashHead = await context.LedgerHeads.FirstAsync(l => l.Code == "1001");
        var loanHead = await context.LedgerHeads.FirstAsync(l => l.Code == "1002");
        var interestHead = await context.LedgerHeads.FirstAsync(l => l.Code == "4001");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var customers = new List<Customer>();
        for (int c = 1; c <= 20; c++)
        {
            customers.Add(new Customer { Code = $"C{c:D4}", Name = $"Customer {c}", MobileNumber = $"98000000{c:D2}" });
        }
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();

        var loans = new List<Loan>();
        var vouchers = new List<Voucher>();
        var random = new Random(100);

        // Generate 500 loans with synchronized vouchers and partial repayments
        for (int i = 1; i <= 500; i++)
        {
            var cust = customers[random.Next(customers.Count)];
            var principal = (decimal)random.Next(10000, 100000);
            var loan = new Loan
            {
                LoanNumber = $"GL-DATA-{i:D5}",
                CustomerId = cust.Id,
                SchemeId = scheme.Id,
                PledgeDate = today.AddDays(-random.Next(1, 200)),
                MaturityDate = today.AddDays(random.Next(-50, 150)),
                LoanAmount = principal,
                GrossWeight = (decimal)random.Next(10, 60),
                NetWeight = (decimal)random.Next(9, 58),
                Status = LoanStatus.Active,
                PacketNumber = $"PKT-{i}"
            };
            loans.Add(loan);

            // Synchronized Loan Disbursement Voucher: Dr 1002 Jewel Loan, Cr 1001 Cash
            var vDisb = new Voucher
            {
                VoucherNumber = $"V-DISB-{i:D5}",
                VoucherDate = loan.PledgeDate,
                VoucherType = VoucherType.Payment,
                TotalDebit = principal,
                TotalCredit = principal,
                Narration = $"Loan {loan.LoanNumber} disbursement",
                IsPosted = true
            };
            vDisb.Lines.Add(new VoucherLine { LedgerHeadId = loanHead.Id, DebitAmount = principal, CreditAmount = 0m, LineOrder = 1 });
            vDisb.Lines.Add(new VoucherLine { LedgerHeadId = cashHead.Id, DebitAmount = 0m, CreditAmount = principal, LineOrder = 2 });
            vouchers.Add(vDisb);
        }

        context.Loans.AddRange(loans);
        context.Vouchers.AddRange(vouchers);
        await context.SaveChangesAsync();

        // 1. Performance benchmark: Dashboard execution time
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var dash = await dashboardService.GetDashboardSummaryAsync();
        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Dashboard KPI queries must execute under 2 seconds");
        dash.TotalActiveLoansCount.Should().Be(500);

        // 2. Performance benchmark: Outstanding Report execution time
        sw.Restart();
        var outstanding = await reportService.GetOutstandingReportAsync(new JlReportFilterDto());
        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(3000, "Outstanding Report query must execute under 3 seconds");
        outstanding.TotalLoans.Should().Be(500);

        // 3. Accounting Integrity Check: Zero discrepancies between Loan Module and Ledger Head 1002
        var integrity = await accountingService.RunIntegrityAuditAsync(today);
        integrity.HasDiscrepancies.Should().BeFalse();
        integrity.UnbalancedVouchersCount.Should().Be(0);
        integrity.LoanModuleVsLedgerDifference.Should().Be(0m);
        integrity.BalanceSheetDifference.Should().Be(0m);
    }
}
