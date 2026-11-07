using System.Diagnostics;
using FluentAssertions;
using JGF.Application.Loans;
using JGF.Application.Reports;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JGF.Tests.Performance;

public class ScreenLoadPerformanceTests
{
    private (JgfDbContext Context, IDbContextFactory<JgfDbContext> Factory) CreateBenchmarkDatabase()
    {
        var dbName = $"JGF_Perf_50k_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .EnableSensitiveDataLogging(false)
            .Options;

        var factory = new TestDbContextFactory(options);
        var context = new JgfDbContext(options);

        // Chart of accounts
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", OpeningBalance = 500_000m },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit" },
            new LedgerHead { Code = "4001", Name = "Jewel Loan Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit" }
        );

        return (context, factory);
    }

    private class TestDbContextFactory : IDbContextFactory<JgfDbContext>
    {
        private readonly DbContextOptions<JgfDbContext> _options;
        public TestDbContextFactory(DbContextOptions<JgfDbContext> options) => _options = options;
        public JgfDbContext CreateDbContext() => new JgfDbContext(_options);
        public Task<JgfDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new JgfDbContext(_options));
    }

    [Fact]
    public async Task Benchmark_MainScreens_MustLoadUnderTwoSeconds_With50000Loans()
    {
        var (context, factory) = CreateBenchmarkDatabase();

        // 1. Seed baseline scheme and customer
        var customer = new Customer
        {
            Code = "C0001",
            Name = "Muthu Kumaran",
            Phone = "9876543210",
            Address = "12 Bazaar Street, Madurai",
            Gender = Gender.Male
        };
        context.Customers.Add(customer);

        var scheme = new Scheme
        {
            Code = "S1",
            Name = "Regular 18% Scheme",
            InterestRatePerMonth = 1.50m,
            MaxTenureMonths = 12,
            GraceDaysAfterExpiry = 7
        };
        context.Schemes.Add(scheme);
        await context.SaveChangesAsync();

        // Seed 50,000 loans
        const int totalLoans = 50_000;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var loans = new List<Loan>(totalLoans);

        for (int i = 1; i <= totalLoans; i++)
        {
            loans.Add(new Loan
            {
                LoanNumber = $"JL{i:D6}",
                CustomerId = customer.Id,
                SchemeId = scheme.Id,
                LoanAmount = 25_000m + (i % 100) * 500m,
                MarketValue = 35_000m,
                GrossWeight = 10.0m,
                NetWeight = 9.5m,
                PledgeDate = today.AddDays(-(i % 365)),
                MaturityDate = today.AddDays(365 - (i % 365)),
                Status = (i % 5 == 0) ? LoanStatus.Closed : LoanStatus.Active
            });
        }

        // Insert in efficient chunks
        const int chunkSize = 10_000;
        for (int i = 0; i < totalLoans; i += chunkSize)
        {
            await context.Loans.AddRangeAsync(loans.Skip(i).Take(chunkSize));
            await context.SaveChangesAsync();
        }

        // Services
        var backupService = new BackupService(context, new AesEncryptionService(), NullLogger<BackupService>.Instance);
        var dashboardService = new DashboardService(factory, backupService);
        var masterDataService = new MasterDataService(factory);
        var reportService = new JlReportService(context, masterDataService, new PermissionService(), new StandardInterestCalculator());

        // 1. Benchmark: Dashboard Screen Load (Home Screen)
        var sw = Stopwatch.StartNew();
        var dashboardResult = await dashboardService.GetDashboardSummaryAsync();
        sw.Stop();

        dashboardResult.Should().NotBeNull();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Dashboard home screen query must complete in under 2 seconds on 50,000 loans");

        // 2. Benchmark: Reports Screen Outstanding Query
        sw.Restart();
        var outstandingReport = await reportService.GetOutstandingReportAsync(new JlReportFilterDto());
        sw.Stop();

        outstandingReport.Should().NotBeNull();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Outstanding Report query must complete in under 2 seconds on 50,000 loans");

        // 3. Benchmark: Customer Master Search
        sw.Restart();
        var customerSearch = await masterDataService.SearchCustomersAsync("Muthu");
        sw.Stop();

        customerSearch.Should().NotBeEmpty();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Customer Master Search must complete in under 2 seconds");
    }
}
