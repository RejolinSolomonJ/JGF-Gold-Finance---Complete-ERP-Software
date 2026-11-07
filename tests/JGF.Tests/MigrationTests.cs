using System.Text;
using FluentAssertions;
using JGF.Application.Accounting;
using JGF.Application.Migration;
using JGF.Application.Security;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JGF.Tests;

public class MigrationTests
{
    private JgfDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: $"JGF_Migration_Test_{Guid.NewGuid()}")
            .Options;

        var context = new JgfDbContext(options);

        // Seed basic accounts
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit" },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit" },
            new LedgerHead { Code = "3001", Name = "Owner's Capital", AccountGroup = LedgerHeadType.Capital, NormalBalance = "Credit" },
            new LedgerHead { Code = "4001", Name = "Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit" }
        );

        context.Schemes.Add(new Scheme
        {
            Code = "S1",
            Name = "Standard Scheme",
            InterestMethod = InterestMethod.Simple,
            InterestRatePerMonth = 1.5m,
            MaxLtvPercentage = 75m,
            MinimumInterestMonths = 1
        });

        context.JewelTypes.Add(new JewelType
        {
            Code = "CH",
            Name = "Gold Chain",
            Description = "Standard Gold Chain",
            IsActive = true
        });

        context.SaveChanges();
        return context;
    }

    private (JgfDbContext Context, IAccountingReportService AccountingService, IDataMigrationService MigrationService) CreateServices()
    {
        var context = CreateInMemoryContext();
        var masterDataService = new MasterDataService(context);
        var permissionService = new PermissionService();
        var accountingService = new AccountingReportService(context, masterDataService, permissionService);
        var migrationService = new DataMigrationService(context, accountingService);

        return (context, accountingService, migrationService);
    }

    [Fact]
    public async Task MigrateCustomers_DryRun_ValidatesWithoutSaving()
    {
        var (context, _, migrationService) = CreateServices();

        var csv = @"CustomerCode,Name,Mobile,Address
C001,Karthik Raja,9876543210,123 Bazaar Street Chennai
C002,Meenakshi Sundaram,9840123456,45 South Car Street Madurai
C003,,9898989898,Missing Name Street
C004,Invalid Phone,1234,Bad Phone Address";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var mapping = new Dictionary<string, string>();

        // Dry-run mode
        var result = await migrationService.MigrateCustomersAsync(stream, "customers.csv", mapping, isDryRun: true);

        result.IsDryRun.Should().BeTrue();
        result.TotalRead.Should().Be(4);
        result.SuccessfullyProcessed.Should().Be(2); // C001, C002 valid
        result.FailedRows.Should().Be(2); // C003 missing name, C004 short mobile

        // Verify nothing was persisted during dry-run
        var count = await context.Customers.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task MigrateCustomers_LiveRun_PersistsValidRecords()
    {
        var (context, _, migrationService) = CreateServices();

        var csv = @"CustomerCode,Name,Mobile,Address
C001,Karthik Raja,9876543210,123 Bazaar Street Chennai
C002,Meenakshi Sundaram,9840123456,45 South Car Street Madurai";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var mapping = new Dictionary<string, string>();

        var result = await migrationService.MigrateCustomersAsync(stream, "customers.csv", mapping, isDryRun: false);

        result.IsDryRun.Should().BeFalse();
        result.SuccessfullyProcessed.Should().Be(2);
        result.FailedRows.Should().Be(0);

        var count = await context.Customers.CountAsync();
        count.Should().Be(2);

        var karthik = await context.Customers.FirstOrDefaultAsync(c => c.Phone == "9876543210");
        karthik.Should().NotBeNull();
        karthik!.Name.Should().Be("Karthik Raja");
    }

    [Fact]
    public async Task MigrateLoans_CreatesLoanAndBalancesVoucher()
    {
        var (context, _, migrationService) = CreateServices();

        // Pre-create customer
        context.Customers.Add(new Customer
        {
            Code = "C001",
            Name = "Annamalai",
            Phone = "9876543210",
            Address = "Main Bazaar"
        });
        await context.SaveChangesAsync();

        var csv = @"LoanNumber,CustomerMobile,PledgeDate,Principal,GrossWeight,NetWeight,JewelName,ItemCount
GL-2026-001,9876543210,2026-10-01,50000,12.5,12.0,22K Gold Chain,1";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var mapping = new Dictionary<string, string>();

        var result = await migrationService.MigrateLoansAsync(stream, "loans.csv", mapping, isDryRun: false);

        result.SuccessfullyProcessed.Should().Be(1);
        result.FailedRows.Should().Be(0);

        var loan = await context.Loans.Include(l => l.Items).FirstOrDefaultAsync(l => l.LoanNumber == "GL-2026-001");
        loan.Should().NotBeNull();
        loan!.LoanAmount.Should().Be(50000m);
        loan.Items.Should().HaveCount(1);
        loan.Items.First().NetWeight.Should().Be(12.0m);

        // Verify voucher lines balance: Debit 1002 (50000) == Credit 3001 (50000)
        var voucher = await context.Vouchers.Include(v => v.Lines).FirstOrDefaultAsync(v => v.VoucherNumber == "VCH-MIG-GL-2026-001");
        voucher.Should().NotBeNull();
        voucher!.TotalDebit.Should().Be(50000m);
        voucher.TotalCredit.Should().Be(50000m);
    }

    [Fact]
    public async Task ReconciliationReport_ComputesZeroDifference_WhenOldMatchesNew()
    {
        var (context, _, migrationService) = CreateServices();

        var customer = new Customer { Code = "C1", Name = "Ramesh", Phone = "9998887770", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "L101",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = new DateOnly(2026, 11, 1),
            MaturityDate = new DateOnly(2027, 11, 1),
            LoanAmount = 75000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);

        var loanHead = await context.LedgerHeads.FirstAsync(h => h.Code == "1002");
        var capHead = await context.LedgerHeads.FirstAsync(h => h.Code == "3001");

        context.Vouchers.Add(new Voucher
        {
            VoucherNumber = "VCH-L101",
            VoucherDate = new DateOnly(2026, 11, 1),
            VoucherType = VoucherType.Journal,
            TotalDebit = 75000m,
            TotalCredit = 75000m,
            Lines = new List<VoucherLine>
            {
                new() { LedgerHeadId = loanHead.Id, LineOrder = 1, DebitAmount = 75000m, CreditAmount = 0m },
                new() { LedgerHeadId = capHead.Id, LineOrder = 2, DebitAmount = 0m, CreditAmount = 75000m }
            }
        });
        await context.SaveChangesAsync();

        // Generate reconciliation report expecting 75000 principal
        var recon = await migrationService.GenerateReconciliationReportAsync(
            expectedPrincipal: 75000m,
            expectedInterest: 0m,
            expectedCash: 0m,
            expectedBankOutstanding: 0m);

        recon.NewOutstandingPrincipal.Should().Be(75000m);
        recon.OutstandingPrincipalDiff.Should().Be(0m);
        recon.IsBalanced.Should().BeTrue();
    }
}
