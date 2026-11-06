using FluentAssertions;
using JGF.Application.Accounting;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JGF.Tests.Accounting;

public class AccountsIntegrityTests
{
    private static JgfDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: $"JgfTestDb_{Guid.NewGuid():N}")
            .Options;

        var context = new JgfDbContext(options);

        // Seed basic Chart of Accounts
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", OpeningBalance = 500000m },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit" },
            new LedgerHead { Code = "2001", Name = "Bank Loan Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit" },
            new LedgerHead { Code = "3001", Name = "Owner Capital", AccountGroup = LedgerHeadType.Capital, NormalBalance = "Credit", OpeningBalance = 500000m },
            new LedgerHead { Code = "4001", Name = "Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit" },
            new LedgerHead { Code = "5001", Name = "Shop Rent", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit" }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task IntegrityAudit_Over200BalancedTransactions_ShouldYieldZeroDiscrepancy()
    {
        using var context = CreateInMemoryContext();
        var permissionService = new PermissionService();
        var masterDataService = new MasterDataService(context);
        var reportService = new AccountingReportService(context, masterDataService, permissionService);

        var cashHead = await context.LedgerHeads.FirstAsync(l => l.Code == "1001");
        var loanHead = await context.LedgerHeads.FirstAsync(l => l.Code == "1002");
        var interestHead = await context.LedgerHeads.FirstAsync(l => l.Code == "4001");
        var rentHead = await context.LedgerHeads.FirstAsync(l => l.Code == "5001");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var random = new Random(42);

        // Generate 200 balanced double-entry transactions
        for (int i = 1; i <= 200; i++)
        {
            var amount = (decimal)random.Next(500, 25000);
            var isDisbursement = (i % 3 == 0);
            var isInterestReceipt = (i % 3 == 1);

            var voucher = new Voucher
            {
                VoucherNumber = $"V/24-25/{i:D5}",
                VoucherDate = today,
                VoucherType = isDisbursement ? VoucherType.Payment : VoucherType.Receipt,
                TotalDebit = amount,
                TotalCredit = amount,
                Narration = $"Simulated transaction #{i}",
                IsPosted = true
            };

            if (isDisbursement)
            {
                // Loan Disbursement: Dr Loan Receivable, Cr Cash
                voucher.Lines.Add(new VoucherLine { LedgerHeadId = loanHead.Id, DebitAmount = amount, CreditAmount = 0m, LineOrder = 1 });
                voucher.Lines.Add(new VoucherLine { LedgerHeadId = cashHead.Id, DebitAmount = 0m, CreditAmount = amount, LineOrder = 2 });

                // Synchronize loan ledger sub-module
                context.Loans.Add(new Loan
                {
                    LoanNumber = $"GL-SIM-{i}",
                    CustomerId = Guid.NewGuid(),
                    SchemeId = Guid.NewGuid(),
                    PledgeDate = today,
                    MaturityDate = today.AddYears(1),
                    LoanAmount = amount,
                    Status = LoanStatus.Active
                });
            }
            else if (isInterestReceipt)
            {
                // Interest Income: Dr Cash, Cr Interest Income
                voucher.Lines.Add(new VoucherLine { LedgerHeadId = cashHead.Id, DebitAmount = amount, CreditAmount = 0m, LineOrder = 1 });
                voucher.Lines.Add(new VoucherLine { LedgerHeadId = interestHead.Id, DebitAmount = 0m, CreditAmount = amount, LineOrder = 2 });
            }
            else
            {
                // Rent Expense: Dr Rent, Cr Cash
                voucher.Lines.Add(new VoucherLine { LedgerHeadId = rentHead.Id, DebitAmount = amount, CreditAmount = 0m, LineOrder = 1 });
                voucher.Lines.Add(new VoucherLine { LedgerHeadId = cashHead.Id, DebitAmount = 0m, CreditAmount = amount, LineOrder = 2 });
            }

            context.Vouchers.Add(voucher);
        }

        await context.SaveChangesAsync();

        // Run full integrity audit
        var audit = await reportService.RunIntegrityAuditAsync(today);

        // Assertions: 100% mathematical integrity
        audit.HasDiscrepancies.Should().BeFalse();
        audit.UnbalancedVouchersCount.Should().Be(0);
        audit.BalanceSheetDifference.Should().Be(0m);
        audit.CashReconciliationDifference.Should().Be(0m);
    }
}

