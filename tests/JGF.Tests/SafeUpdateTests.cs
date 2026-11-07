using FluentAssertions;
using JGF.Application.Accounting;
using JGF.Application.Reports;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Application.Updates;
using JGF.Domain.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JGF.Tests;

public class SafeUpdateTests
{
    private class FailingEncryptionService : IAesEncryptionService
    {
        public Task EncryptFileAsync(string inputFilePath, string outputEncryptedFilePath, string keyPhrase, CancellationToken ct = default)
        {
            throw new IOException("Simulated I/O disk hardware failure during pre-update backup snapshot creation.");
        }

        public Task DecryptFileAsync(string encryptedFilePath, string outputDecryptedFilePath, string keyPhrase, CancellationToken ct = default)
        {
            throw new IOException("Decryption error.");
        }
    }

    private class StubBackupService : IBackupService
    {
        public Task<BackupResult> CreateBackupAsync(string encryptionKey, BackupType type = BackupType.Manual, Guid? userId = null, CancellationToken ct = default)
        {
            return Task.FromResult(new BackupResult(true, "C:\\Fake\\backup.enc", 1024));
        }

        public Task<List<BackupLog>> GetBackupLogsAsync(int limit = 50, CancellationToken ct = default)
        {
            return Task.FromResult(new List<BackupLog>());
        }

        public Task<bool> HasRecentSuccessfulBackupAsync(TimeSpan threshold, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RestoreBackupAsync(string encryptedBackupPath, string encryptionKey, string targetDatabaseName, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }
    }

    private class StubAccountingReportService : IAccountingReportService
    {
        public Task<BalanceSheetReportDto> GetBalanceSheetAsync(DateOnly asOfDate, CancellationToken ct = default)
        {
            return Task.FromResult(new BalanceSheetReportDto(asOfDate, new(), 0, new(), 0, new(), 0, 0, 0, 0));
        }

        public Task<DayBookSummaryDto> GetDayBookAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
        {
            return Task.FromResult(new DayBookSummaryDto(fromDate, toDate, 0, 0, 0, 0, new()));
        }

        public Task<ProfitAndLossReportDto> GetProfitAndLossAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
        {
            return Task.FromResult(new ProfitAndLossReportDto(fromDate, toDate, new(), 0, new(), 0, 0));
        }

        public Task<TrialBalanceReportDto> GetTrialBalanceAsync(DateOnly asOfDate, CancellationToken ct = default)
        {
            return Task.FromResult(new TrialBalanceReportDto(asOfDate, 0, 0, 0, new()));
        }

        public Task<string> PostManualVoucherAsync(CreateManualVoucherDto dto, CancellationToken ct = default)
        {
            return Task.FromResult("V0001");
        }

        public Task<AccountingIntegrityCheckDto> RunIntegrityAuditAsync(DateOnly asOfDate, CancellationToken ct = default)
        {
            return Task.FromResult(new AccountingIntegrityCheckDto(
                HasDiscrepancies: false,
                UnbalancedVouchersCount: 0,
                UnbalancedVoucherDetails: new(),
                BalanceSheetDifference: 0m,
                CashBookClosingCash: 1000m,
                BalanceSheetCashAsset: 1000m,
                CashReconciliationDifference: 0m,
                LoanModulePrincipalOutstanding: 50000m,
                JewelLoanLedgerHeadBalance: 50000m,
                LoanModuleVsLedgerDifference: 0m,
                BankRepledgeModuleOutstanding: 0m,
                BankRepledgeLedgerHeadBalance: 0m,
                BankRepledgeVsLedgerDifference: 0m
            ));
        }
    }

    private JgfDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new JgfDbContext(options);

        // Core system ledger heads
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
            new LedgerHead { Code = "3001", Name = "Owner's Capital", AccountGroup = LedgerHeadType.Capital, NormalBalance = "Credit", IsSystemAccount = true },
            new LedgerHead { Code = "4001", Name = "Jewel Loan Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit", IsSystemAccount = true }
        );
        context.SaveChanges();

        return context;
    }

    [Fact]
    public void SemanticVersion_Comparison_Rules_WorkCorrectly()
    {
        var v100 = SemanticVersion.Parse("1.0.0");
        var v110 = SemanticVersion.Parse("1.1.0");
        var v120 = SemanticVersion.Parse("1.2.0");
        var v200 = SemanticVersion.Parse("2.0.0");
        var v120Beta = SemanticVersion.Parse("1.2.0-beta");

        (v110 < v120).Should().BeTrue();
        (v120 > v110).Should().BeTrue();
        (v120 == SemanticVersion.Parse("1.2.0")).Should().BeTrue();
        (v200 > v120).Should().BeTrue();
        (v120Beta < v120).Should().BeTrue();
        (v100 < v110).Should().BeTrue();
    }

    [Fact]
    public async Task VersionCheck_WhenAppOlderThanDatabase_BlocksLogin()
    {
        using var context = CreateInMemoryContext("JGF_Update_OlderApp_" + Guid.NewGuid());
        context.SchemaVersions.Add(new SchemaVersion
        {
            AppVersion = "1.2.0",
            AppliedMigrations = "Initial,AddFeatures",
            AppliedAtUtc = DateTime.UtcNow,
            AppliedBy = "Admin",
            IsSuccess = true
        });
        await context.SaveChangesAsync();

        var service = new SafeUpdateService(
            context,
            new StubBackupService(),
            new AesEncryptionService(),
            new StubAccountingReportService(),
            NullLogger<SafeUpdateService>.Instance);

        // App running 1.1.0 against DB at 1.2.0
        var status = await service.CheckVersionStatusAsync(currentAppVersionOverride: "1.1.0");

        status.Status.Should().Be(VersionComparisonResult.AppOlderThanDb);
        status.CanCashierLogin.Should().BeFalse();
        status.CanOwnerManagerUpgrade.Should().BeFalse();
        status.Message.Should().Contain("Please update this PC to version 1.2.0");
    }

    [Fact]
    public async Task VersionCheck_WhenAppNewerThanDatabase_BlocksCashierAndAllowsOwner()
    {
        using var context = CreateInMemoryContext("JGF_Update_NewerApp_" + Guid.NewGuid());
        context.SchemaVersions.Add(new SchemaVersion
        {
            AppVersion = "1.1.0",
            AppliedMigrations = "Initial",
            AppliedAtUtc = DateTime.UtcNow,
            AppliedBy = "Admin",
            IsSuccess = true
        });
        await context.SaveChangesAsync();

        var service = new SafeUpdateService(
            context,
            new StubBackupService(),
            new AesEncryptionService(),
            new StubAccountingReportService(),
            NullLogger<SafeUpdateService>.Instance);

        // App running 1.2.0 against DB at 1.1.0
        var status = await service.CheckVersionStatusAsync(currentAppVersionOverride: "1.2.0");

        status.Status.Should().Be(VersionComparisonResult.AppNewerThanDb);
        status.CanCashierLogin.Should().BeFalse();
        status.CanOwnerManagerUpgrade.Should().BeTrue();
        status.HasPendingMigrations.Should().BeTrue();
        status.Message.Should().Contain("System update pending, contact the owner");
    }

    [Fact]
    public async Task PC2_Client_OnOlderVersion_IsBlockedFromLogin()
    {
        using var context = CreateInMemoryContext("JGF_Update_PC2Older_" + Guid.NewGuid());
        context.SchemaVersions.Add(new SchemaVersion
        {
            AppVersion = "1.2.0",
            AppliedMigrations = "V120_Migration",
            AppliedAtUtc = DateTime.UtcNow,
            AppliedBy = "Owner",
            IsSuccess = true
        });
        await context.SaveChangesAsync();

        var service = new SafeUpdateService(
            context,
            new StubBackupService(),
            new AesEncryptionService(),
            new StubAccountingReportService(),
            NullLogger<SafeUpdateService>.Instance);

        // PC2 workstation has not updated yet (runs 1.1.0)
        var status = await service.CheckVersionStatusAsync(currentAppVersionOverride: "1.1.0");

        status.CanCashierLogin.Should().BeFalse();
        status.Message.Should().Be("Please update this PC to version 1.2.0. (This PC is running version 1.1.0)");
    }

    [Fact]
    public async Task Update_WhenPreUpdateBackupFails_AbortsImmediatelyWithoutModifyingDatabase()
    {
        using var context = CreateInMemoryContext("JGF_Update_BackupFailure_" + Guid.NewGuid());
        context.SchemaVersions.Add(new SchemaVersion
        {
            AppVersion = "1.1.0",
            AppliedMigrations = "Initial",
            AppliedAtUtc = DateTime.UtcNow,
            AppliedBy = "Admin",
            IsSuccess = true
        });
        await context.SaveChangesAsync();

        var initialCount = await context.SchemaVersions.CountAsync();

        // Use failing encryption service that throws during pre-update backup
        var service = new SafeUpdateService(
            context,
            new StubBackupService(),
            new FailingEncryptionService(),
            new StubAccountingReportService(),
            NullLogger<SafeUpdateService>.Instance);

        // Attempt update
        var result = await service.ExecuteUpdateAsync(
            targetVersion: "1.2.0",
            appliedByUser: "Admin",
            encryptionKey: "TestPass123");

        // Verify update was aborted with ZERO changes
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MANDATORY pre-update backup failed");

        var finalCount = await context.SchemaVersions.CountAsync();
        finalCount.Should().Be(initialCount);

        var latestVersion = (await context.SchemaVersions.OrderByDescending(s => s.AppliedAtUtc).FirstAsync()).AppVersion;
        latestVersion.Should().Be("1.1.0"); // Remained at 1.1.0!
    }

    [Fact]
    public async Task IntegrationTest_ApplyMigration_ToDatabaseWith10000Loans_MaintainsAccountingIntegrity()
    {
        using var context = CreateInMemoryContext("JGF_Update_10000Loans_" + Guid.NewGuid());
        context.SchemaVersions.Add(new SchemaVersion
        {
            AppVersion = "1.1.0",
            AppliedMigrations = "Initial_Create",
            AppliedAtUtc = DateTime.UtcNow.AddMonths(-1),
            AppliedBy = "Admin",
            IsSuccess = true
        });

        // 1. Seed 10,000 active customer loans and vouchers
        var customer = new Customer
        {
            Code = "C0001",
            FullName = "Ramasamy M",
            MobileNumber = "9876543210",
            Address = "Madurai Main Road"
        };
        context.Customers.Add(customer);
        context.SaveChanges();

        var loans = new List<Loan>(10000);
        var baseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2));
        decimal totalPrincipal = 0m;

        for (int i = 1; i <= 10000; i++)
        {
            var amount = 10000m + (i % 500) * 100m;
            totalPrincipal += amount;

            loans.Add(new Loan
            {
                LoanNumber = $"GL{i:D6}",
                CustomerId = customer.Id,
                PledgeDate = baseDate,
                MaturityDate = baseDate.AddYears(1),
                LoanAmount = amount,
                Status = LoanStatus.Active,
                SchemeId = Guid.NewGuid()
            });
        }

        context.Loans.AddRange(loans);

        // Vouchers balancing the 10,000 loans:
        // Debit: Jewel Loan Receivable (1002) = totalPrincipal
        // Credit: Cash on Hand (1001) = totalPrincipal
        var headCash = await context.LedgerHeads.FirstAsync(h => h.Code == "1001");
        var headLoan = await context.LedgerHeads.FirstAsync(h => h.Code == "1002");
        var headCapital = await context.LedgerHeads.FirstAsync(h => h.Code == "3001");

        var vCapital = new Voucher
        {
            VoucherNumber = "V0001",
            VoucherDate = baseDate,
            VoucherType = VoucherType.Receipt,
            Narration = "Capital contribution",
            TotalDebit = totalPrincipal,
            TotalCredit = totalPrincipal,
            IsPosted = true,
            Lines = new List<VoucherLine>
            {
                new() { LedgerHeadId = headCash.Id, DebitAmount = totalPrincipal, CreditAmount = 0m, LineOrder = 1, Narration = "Cash received" },
                new() { LedgerHeadId = headCapital.Id, DebitAmount = 0m, CreditAmount = totalPrincipal, LineOrder = 2, Narration = "Capital credit" }
            }
        };

        var vDisburse = new Voucher
        {
            VoucherNumber = "V0002",
            VoucherDate = baseDate,
            VoucherType = VoucherType.Payment,
            Narration = "Bulk disbursal of 10,000 loans",
            TotalDebit = totalPrincipal,
            TotalCredit = totalPrincipal,
            IsPosted = true,
            Lines = new List<VoucherLine>
            {
                new() { LedgerHeadId = headLoan.Id, DebitAmount = totalPrincipal, CreditAmount = 0m, LineOrder = 1, Narration = "Loans" },
                new() { LedgerHeadId = headCash.Id, DebitAmount = 0m, CreditAmount = totalPrincipal, LineOrder = 2, Narration = "Cash disbursed" }
            }
        };
        context.Vouchers.AddRange(vCapital, vDisburse);
        context.SaveChanges();

        // 2. Setup SafeUpdateService with valid AesEncryptionService & actual accounting service
        var masterDataService = new MasterDataService(context);
        var reportService = new AccountingReportService(context, masterDataService, new PermissionService());

        var updateService = new SafeUpdateService(
            context,
            new StubBackupService(),
            new AesEncryptionService(),
            reportService,
            NullLogger<SafeUpdateService>.Instance);

        // 3. Execute safe upgrade to version 1.2.0
        var updateResult = await updateService.ExecuteUpdateAsync(
            targetVersion: "1.2.0",
            appliedByUser: "Admin (Owner)",
            encryptionKey: "SafeMasterKey2026!#");

        // 4. Assert upgrade succeeded and audit reports ZERO discrepancies across all 10,000 loans
        updateResult.Success.Should().BeTrue();
        updateResult.TargetVersion.Should().Be("1.2.0");
        updateResult.IntegrityAuditResult.Should().NotBeNull();
        updateResult.IntegrityAuditResult!.HasDiscrepancies.Should().BeFalse();
        updateResult.IntegrityAuditResult.UnbalancedVouchersCount.Should().Be(0);
        updateResult.IntegrityAuditResult.LoanModuleVsLedgerDifference.Should().Be(0m);
        updateResult.IntegrityAuditResult.LoanModulePrincipalOutstanding.Should().Be(totalPrincipal);

        // Confirm SchemaVersions table contains the new version
        var latestSchema = await context.SchemaVersions.OrderByDescending(s => s.AppliedAtUtc).FirstAsync();
        latestSchema.AppVersion.Should().Be("1.2.0");
        latestSchema.IsSuccess.Should().BeTrue();
        latestSchema.IntegrityCheckStatus.Should().Contain("PASSED (Zero Discrepancies)");
    }
}
