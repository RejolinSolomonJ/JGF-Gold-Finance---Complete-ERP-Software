using FluentAssertions;
using JGF.Application.Repledge;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JGF.Tests.Repledge;

public class BankRepledgeTests
{
    private static JgfDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: $"JgfRepledgeTestDb_{Guid.NewGuid():N}")
            .Options;

        var context = new JgfDbContext(options);

        // Seed basic accounts
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", OpeningBalance = 500000m },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit" },
            new LedgerHead { Code = "2001", Name = "Bank Loan Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit" },
            new LedgerHead { Code = "5001", Name = "Bank Interest Expense", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit" },
            new LedgerHead { Code = "5004", Name = "Postal Charges Expense", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit" }
        );

        // Seed customer and bank
        var bank = new Bank { Code = "SBI", Name = "State Bank of India", InterestRate = 0.75m, IsActive = true };
        var customer = new Customer { Code = "C0001", FullName = "Murugan S", MobileNumber = "9876543210" };

        context.Banks.Add(bank);
        context.Customers.Add(customer);
        context.SaveChanges();

        // Seed customer loan
        var loan = new Loan
        {
            LoanNumber = "S1/24-25/0001",
            CustomerId = customer.Id,
            SchemeId = Guid.NewGuid(),
            PledgeDate = new DateOnly(2024, 6, 15),
            LoanAmount = 76000m,
            MarketValue = 101750m,
            NetWeight = 18.500m,
            GrossWeight = 20.000m,
            Status = LoanStatus.Active,
            PacketNumber = "PKT-001"
        };
        context.Loans.Add(loan);
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task CreateBankPledge_ShouldMarkJewelsInBankCustody_AndPostBalancedVouchers()
    {
        using var context = CreateInMemoryContext();
        var permissionService = new PermissionService();
        var masterDataService = new MasterDataService(context);
        var repledgeService = new BankRepledgeService(context, masterDataService, permissionService);

        var bank = await context.Banks.FirstAsync();
        var loan = await context.Loans.FirstAsync();

        var dto = new CreateBankPledgeDto(
            bank.Id,
            "BP-SBI-24001",
            new DateOnly(2024, 7, 1),
            new DateOnly(2025, 7, 1),
            50000m, // Bank lends ₹50,000 against customer loan of ₹76,000
            0.75m,
            18.500m,
            "SBIN-LN-99",
            "JGF Bankers",
            "REC-SBI-01",
            new List<Guid> { loan.Id },
            "Refinance",
            Guid.Empty,
            UserRole.Owner
        );

        var result = await repledgeService.CreateBankPledgeAsync(dto);

        result.Should().NotBeNull();
        result.DisbursedAmount.Should().Be(50000m);
        result.Status.Should().Be(BankPledgeStatus.Active);

        // Verify custody is now in Bank
        var isWithBank = await repledgeService.IsLoanInBankCustodyAsync(loan.Id);
        isWithBank.Should().BeTrue();

        // Verify stock custody report separates locker vs bank
        var stockReport = await repledgeService.GetJewelStockCustodyReportAsync();
        stockReport.Should().Contain(s => s.CustodyLocation.StartsWith("In Bank Custody (State Bank of India"));

        // Verify double-entry voucher posted
        var voucher = await context.Vouchers.Include(v => v.Lines).FirstOrDefaultAsync(v => v.ReferenceId == result.Id);
        voucher.Should().NotBeNull();
        voucher!.TotalDebit.Should().Be(50000m);
        voucher.TotalCredit.Should().Be(50000m);
        voucher.Lines.Sum(l => l.DebitAmount).Should().Be(50000m);
        voucher.Lines.Sum(l => l.CreditAmount).Should().Be(50000m);
    }

    [Fact]
    public async Task ProcessBankPayment_WithPartialRelease_ShouldIssueRzReceiptAndReturnJewelsToLocker()
    {
        using var context = CreateInMemoryContext();
        var permissionService = new PermissionService();
        var masterDataService = new MasterDataService(context);
        var repledgeService = new BankRepledgeService(context, masterDataService, permissionService);

        var bank = await context.Banks.FirstAsync();
        var loan = await context.Loans.FirstAsync();

        // First create bank pledge
        var createDto = new CreateBankPledgeDto(
            bank.Id, "BP-SBI-002", new DateOnly(2024, 7, 1), new DateOnly(2025, 7, 1),
            50000m, 0.75m, 18.500m, "SBIN-02", "JGF", null, new List<Guid> { loan.Id }, null, Guid.Empty, UserRole.Owner);

        var pledge = await repledgeService.CreateBankPledgeAsync(createDto);

        // Pay full principal + interest to release loan
        var payDto = new SettleBankPaymentDto(
            pledge.Id,
            new DateOnly(2024, 8, 1),
            BankPaymentType.FullRelease,
            PrincipalPaid: 50000m,
            InterestPaid: 375m, // 50,000 * 0.75% = ₹375
            PostalCharges: 50m,
            ReleasedLoanIds: new List<Guid> { loan.Id },
            ReferenceNumber: "CHQ-12345",
            Remarks: "Loan fully released",
            Guid.Empty,
            UserRole.Owner
        );

        var (payment, rzNumber) = await repledgeService.ProcessBankPaymentAsync(payDto);

        rzNumber.Should().StartWith("RZ/24-25/");
        payment.TotalAmount.Should().Be(50425m);

        // Verify jewels are now returned to shop locker
        var isStillWithBank = await repledgeService.IsLoanInBankCustodyAsync(loan.Id);
        isStillWithBank.Should().BeFalse();

        // Verify voucher balance
        var v = await context.Vouchers.Include(x => x.Lines).FirstOrDefaultAsync(x => x.ReferenceId == payment.Id);
        v.Should().NotBeNull();
        v!.TotalDebit.Should().Be(50425m);
        v.TotalCredit.Should().Be(50425m);
        v.Lines.Sum(l => l.DebitAmount).Should().Be(50425m);
        v.Lines.Sum(l => l.CreditAmount).Should().Be(50425m);
    }
}

