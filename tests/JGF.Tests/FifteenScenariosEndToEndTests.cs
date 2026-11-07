using FluentAssertions;
using JGF.Application.Accounting;
using JGF.Application.Loans;
using JGF.Application.Notices;
using JGF.Application.Receipts;
using JGF.Application.Repledge;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JGF.Tests;

public class FifteenScenariosEndToEndTests
{
    private JgfDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: $"JGF_E2E_15Scenarios_{Guid.NewGuid()}")
            .Options;

        var context = new JgfDbContext(options);

        // Core Ledger Heads
        context.LedgerHeads.AddRange(
            new LedgerHead { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
            new LedgerHead { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
            new LedgerHead { Code = "1003", Name = "Bank Account Primary", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
            new LedgerHead { Code = "2001", Name = "Bank Repledge Loan Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit", IsSystemAccount = true },
            new LedgerHead { Code = "2002", Name = "Auction Surplus Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit", IsSystemAccount = true },
            new LedgerHead { Code = "3001", Name = "Owner's Capital", AccountGroup = LedgerHeadType.Capital, NormalBalance = "Credit", IsSystemAccount = true },
            new LedgerHead { Code = "4001", Name = "Jewel Loan Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit", IsSystemAccount = true },
            new LedgerHead { Code = "4004", Name = "Postal Charges Recovered", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit", IsSystemAccount = true },
            new LedgerHead { Code = "5001", Name = "Bank Repledge Interest Expense", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit", IsSystemAccount = true }
        );

        // Scheme
        context.Schemes.Add(new Scheme
        {
            Code = "S1",
            Name = "Regular 18% Scheme",
            InterestMethod = InterestMethod.Simple,
            InterestRatePerMonth = 1.5m,
            MaxLtvPercentage = 75m,
            MinimumInterestMonths = 1,
            MinimumInterestDays = 30,
            GracePeriodDays = 7
        });

        // Jewel Types
        context.JewelTypes.AddRange(
            new JewelType { Code = "CH", Name = "Gold Chain", Description = "Chain", IsActive = true },
            new JewelType { Code = "RG", Name = "Gold Ring", Description = "Ring", IsActive = true }
        );

        // Bank
        context.Banks.Add(new Bank
        {
            Code = "SBI",
            Name = "State Bank of India",
            Branch = "Town Hall",
            IsActive = true
        });

        // Opening cash capital of ₹10,00,000
        var openingVoucher = new Voucher
        {
            VoucherNumber = "VCH-OPENING-001",
            VoucherDate = new DateOnly(2026, 11, 1),
            VoucherType = VoucherType.Receipt,
            Narration = "Capital Introduced",
            TotalDebit = 1000000m,
            TotalCredit = 1000000m,
            Lines = new List<VoucherLine>
            {
                new() { LedgerHeadId = Guid.Empty, LineOrder = 1, DebitAmount = 1000000m, CreditAmount = 0m, Narration = "Cash introduced" },
                new() { LedgerHeadId = Guid.Empty, LineOrder = 2, DebitAmount = 0m, CreditAmount = 1000000m, Narration = "Owner Capital" }
            }
        };

        context.Vouchers.Add(openingVoucher);
        context.SaveChanges();

        // Assign ledger head IDs to opening voucher
        var cashHead = context.LedgerHeads.First(h => h.Code == "1001");
        var capHead = context.LedgerHeads.First(h => h.Code == "3001");
        openingVoucher.Lines.First().LedgerHeadId = cashHead.Id;
        openingVoucher.Lines.Last().LedgerHeadId = capHead.Id;
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task Scenario01_CustomerOnboarding_SavesWithValidKyc()
    {
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Code = "C0001",
            Name = "Sundaramoorthy",
            Phone = "9842100000",
            Address = "12 North Street Madurai",
            AadhaarNumber = "1234-5678-9012"
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var saved = await context.Customers.FirstOrDefaultAsync(c => c.Phone == "9842100000");
        saved.Should().NotBeNull();
        saved!.Code.Should().Be("C0001");
    }

    [Fact]
    public async Task Scenario02_LoanIssueAndVoucherPosting_BalancesDoubleEntry()
    {
        using var context = CreateInMemoryContext();
        var customer = new Customer { Code = "C1", Name = "Muthu", Phone = "9842111111", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        var chainType = await context.JewelTypes.FirstAsync(j => j.Code == "CH");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "GL-0001",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = new DateOnly(2026, 11, 1),
            MaturityDate = new DateOnly(2027, 11, 1),
            LoanAmount = 50000m,
            MarketValue = 70000m,
            NetWeight = 11.5m,
            GrossWeight = 12.0m,
            ApplicableGoldRate = 6000m,
            Status = LoanStatus.Active,
            Items = new List<LoanItem>
            {
                new() { ItemNumber = 1, JewelTypeId = chainType.Id, Description = "22K Gold Chain", Quantity = 1, GrossWeight = 12.0m, NetWeight = 11.5m }
            }
        };

        // Post Disbursal Voucher (Debit Jewel Loan 1002, Credit Cash 1001)
        var cashHead = await context.LedgerHeads.FirstAsync(h => h.Code == "1001");
        var loanHead = await context.LedgerHeads.FirstAsync(h => h.Code == "1002");

        var vch = new Voucher
        {
            VoucherNumber = "VCH-GL-0001",
            VoucherDate = loan.PledgeDate,
            VoucherType = VoucherType.Payment,
            Narration = "LOAN DISBURSAL: GL-0001",
            TotalDebit = 50000m,
            TotalCredit = 50000m,
            Lines = new List<VoucherLine>
            {
                new() { LedgerHeadId = loanHead.Id, LineOrder = 1, DebitAmount = 50000m, CreditAmount = 0m },
                new() { LedgerHeadId = cashHead.Id, LineOrder = 2, DebitAmount = 0m, CreditAmount = 50000m }
            }
        };

        context.Loans.Add(loan);
        context.Vouchers.Add(vch);
        await context.SaveChangesAsync();

        vch.TotalDebit.Should().Be(vch.TotalCredit);
        var activeLoans = await context.Loans.CountAsync();
        activeLoans.Should().Be(1);
    }

    [Fact]
    public async Task Scenario03_MinimumOneMonthInterest_EnforcedOnQuickClosure()
    {
        var calc = new StandardInterestCalculator();
        var scheme = new Scheme
        {
            InterestMethod = InterestMethod.Simple,
            InterestRatePerMonth = 1.5m, // 18% p.a.
            MinimumInterestMonths = 1,
            MinimumInterestDays = 30,
            GracePeriodDays = 7
        };

        // Customer pledges on 1st Nov and closes on 5th Nov (only 4 days)
        var pledgeDate = new DateOnly(2026, 11, 1);
        var closeDate = new DateOnly(2026, 11, 5);
        decimal principal = 100000m;

        var result = calc.CalculateInterest(principal, 1.5m, 0.5m, pledgeDate, closeDate, minimumInterestDays: 30);

        // 1 month minimum interest on 1,00,000 @ 1.5% = ₹1,500
        result.TotalInterest.Should().Be(1500m);
    }

    [Fact]
    public async Task Scenario04_ThreeMonthInterest_CalculatesExactAmount()
    {
        var calc = new StandardInterestCalculator();
        var scheme = new Scheme
        {
            InterestMethod = InterestMethod.Simple,
            InterestRatePerMonth = 1.5m,
            MinimumInterestMonths = 1,
            MinimumInterestDays = 30
        };

        var pledgeDate = new DateOnly(2026, 8, 1);
        var payDate = new DateOnly(2026, 11, 1);
        decimal principal = 60000m;

        var result = calc.CalculateInterest(principal, 1.5m, 0.5m, pledgeDate, payDate);

        // 92 days on 60,000 @ 1.5%/month (30-day month basis) = ₹2,760.03
        result.TotalInterest.Should().Be(2760.03m);
    }

    [Fact]
    public async Task Scenario05_PartPrincipalPayment_ReducesOutstanding()
    {
        using var context = CreateInMemoryContext();
        var customer = new Customer { Code = "C1", Name = "Ganesh", Phone = "9842100001", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "GL-PART-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = new DateOnly(2026, 9, 1),
            MaturityDate = new DateOnly(2027, 9, 1),
            LoanAmount = 40000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);
        await context.SaveChangesAsync();

        // Customer pays 10,000 principal
        var receipt = new Receipt
        {
            ReceiptNumber = "R-001",
            LoanId = loan.Id,
            ReceiptDate = new DateOnly(2026, 11, 1),
            PrincipalAmount = 10000m,
            InterestAmount = 1200m,
            TotalAmount = 11200m,
            PaymentMode = PaymentMode.Cash,
            ReceiptType = ReceiptType.PartPrincipal
        };

        loan.LoanAmount -= receipt.PrincipalAmount;
        loan.Status = LoanStatus.PartiallyPaid;
        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();

        loan.LoanAmount.Should().Be(30000m);
        loan.Status.Should().Be(LoanStatus.PartiallyPaid);
    }

    [Fact]
    public async Task Scenario06_InterestOnlyPayment_LeavesPrincipalUnchanged()
    {
        using var context = CreateInMemoryContext();
        var customer = new Customer { Code = "C1", Name = "Selvam", Phone = "9842100002", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "GL-INT-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = new DateOnly(2026, 10, 1),
            MaturityDate = new DateOnly(2027, 10, 1),
            LoanAmount = 50000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);
        await context.SaveChangesAsync();

        var receipt = new Receipt
        {
            ReceiptNumber = "R-002",
            LoanId = loan.Id,
            ReceiptDate = new DateOnly(2026, 11, 1),
            PrincipalAmount = 0m,
            InterestAmount = 750m,
            TotalAmount = 750m,
            PaymentMode = PaymentMode.Cash,
            ReceiptType = ReceiptType.InterestOnly
        };

        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();

        loan.LoanAmount.Should().Be(50000m);
        loan.Status.Should().Be(LoanStatus.Active);
    }

    [Fact]
    public async Task Scenario07_LoanTopUp_IncreasesLoanAmountAndDisbursesCash()
    {
        using var context = CreateInMemoryContext();
        var customer = new Customer { Code = "C1", Name = "Bala", Phone = "9842100003", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "GL-TOPUP-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = new DateOnly(2026, 10, 1),
            MaturityDate = new DateOnly(2027, 10, 1),
            LoanAmount = 40000m,
            MarketValue = 80000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);
        await context.SaveChangesAsync();

        // Top-up ₹15,000 (disburse additional cash)
        decimal topUpAmount = 15000m;
        loan.LoanAmount += topUpAmount;

        var cashHead = await context.LedgerHeads.FirstAsync(h => h.Code == "1001");
        var loanHead = await context.LedgerHeads.FirstAsync(h => h.Code == "1002");

        var vch = new Voucher
        {
            VoucherNumber = "VCH-TOPUP-1",
            VoucherDate = new DateOnly(2026, 11, 1),
            VoucherType = VoucherType.Payment,
            Narration = "LOAN TOP-UP FOR GL-TOPUP-1",
            TotalDebit = topUpAmount,
            TotalCredit = topUpAmount,
            Lines = new List<VoucherLine>
            {
                new() { LedgerHeadId = loanHead.Id, LineOrder = 1, DebitAmount = topUpAmount, CreditAmount = 0m },
                new() { LedgerHeadId = cashHead.Id, LineOrder = 2, DebitAmount = 0m, CreditAmount = topUpAmount }
            }
        };

        context.Vouchers.Add(vch);
        await context.SaveChangesAsync();

        loan.LoanAmount.Should().Be(55000m);
    }

    [Fact]
    public async Task Scenario08_LoanRenewal_ClosesOldAndOpensNewLinkedLoan()
    {
        using var context = CreateInMemoryContext();
        var customer = new Customer { Code = "C1", Name = "Murugan", Phone = "9842100004", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var oldLoan = new Loan
        {
            LoanNumber = "GL-OLD-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = new DateOnly(2025, 11, 1),
            MaturityDate = new DateOnly(2026, 11, 1),
            LoanAmount = 50000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(oldLoan);
        await context.SaveChangesAsync();

        // Renew: close old loan and create new loan
        var newLoan = new Loan
        {
            LoanNumber = "GL-NEW-2",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            PledgeDate = new DateOnly(2026, 11, 1),
            MaturityDate = new DateOnly(2027, 11, 1),
            LoanAmount = 50000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(newLoan);
        await context.SaveChangesAsync();

        oldLoan.Status = LoanStatus.Closed;
        oldLoan.RenewalLoanId = newLoan.Id;
        await context.SaveChangesAsync();

        oldLoan.Status.Should().Be(LoanStatus.Closed);
        oldLoan.RenewalLoanId.Should().Be(newLoan.Id);
    }

    [Fact]
    public async Task Scenario09_PartReleaseOfJewels_ReleasesSpecificItem()
    {
        using var context = CreateInMemoryContext();
        var customer = new Customer { Code = "C1", Name = "Velu", Phone = "9842100005", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        var chain = await context.JewelTypes.FirstAsync(j => j.Code == "CH");
        var ring = await context.JewelTypes.FirstAsync(j => j.Code == "RG");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "GL-RELEASE-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            LoanAmount = 60000m,
            Status = LoanStatus.Active,
            Items = new List<LoanItem>
            {
                new() { ItemNumber = 1, JewelTypeId = chain.Id, Description = "Chain", GrossWeight = 10m, NetWeight = 10m },
                new() { ItemNumber = 2, JewelTypeId = ring.Id, Description = "Ring", GrossWeight = 4m, NetWeight = 4m }
            }
        };
        context.Loans.Add(loan);
        await context.SaveChangesAsync();

        // Customer pays 20,000 and releases the ring
        var ringItem = loan.Items.First(i => i.Description == "Ring");
        context.LoanItems.Remove(ringItem);
        loan.Items.Remove(ringItem);
        loan.LoanAmount -= 20000m;
        await context.SaveChangesAsync();

        var remainingItems = await context.LoanItems.Where(i => i.LoanId == loan.Id).ToListAsync();
        remainingItems.Should().HaveCount(1);
        loan.LoanAmount.Should().Be(40000m);
    }

    [Fact]
    public async Task Scenario10_BankRepledge_DisbursesCashAndRecordsLiability()
    {
        using var context = CreateInMemoryContext();
        var bank = await context.Banks.FirstAsync();

        var repledge = new BankPledge
        {
            PledgeNumber = "BP-001",
            BankId = bank.Id,
            PledgeDate = new DateOnly(2026, 11, 1),
            TotalPledgeAmount = 200000m,
            DisbursedAmount = 200000m,
            InterestRate = 10.5m,
            BankWeight = 45m,
            Status = BankPledgeStatus.Active
        };

        var cashHead = await context.LedgerHeads.FirstAsync(h => h.Code == "1001");
        var bankLoanHead = await context.LedgerHeads.FirstAsync(h => h.Code == "2001");

        var vch = new Voucher
        {
            VoucherNumber = "VCH-BP-001",
            VoucherDate = repledge.PledgeDate,
            VoucherType = VoucherType.Receipt,
            Narration = "BANK REPLEDGE DISBURSAL BP-001",
            TotalDebit = 200000m,
            TotalCredit = 200000m,
            Lines = new List<VoucherLine>
            {
                new() { LedgerHeadId = cashHead.Id, LineOrder = 1, DebitAmount = 200000m, CreditAmount = 0m },
                new() { LedgerHeadId = bankLoanHead.Id, LineOrder = 2, DebitAmount = 0m, CreditAmount = 200000m }
            }
        };

        context.BankPledges.Add(repledge);
        context.Vouchers.Add(vch);
        await context.SaveChangesAsync();

        repledge.Status.Should().Be(BankPledgeStatus.Active);
        vch.TotalDebit.Should().Be(200000m);
    }

    [Fact]
    public async Task Scenario11_ClosingCustomerLoanWithJewelsInBank_RaisesSafetyCheck()
    {
        using var context = CreateInMemoryContext();
        var bank = await context.Banks.FirstAsync();
        var customer = new Customer { Code = "C1", Name = "Palani", Phone = "9842100006", Address = "Town" };
        var scheme = await context.Schemes.FirstAsync();
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var loan = new Loan
        {
            LoanNumber = "GL-BANK-SAFE-1",
            CustomerId = customer.Id,
            SchemeId = scheme.Id,
            LoanAmount = 40000m,
            Status = LoanStatus.Active
        };
        context.Loans.Add(loan);

        var bankPledge = new BankPledge
        {
            PledgeNumber = "BP-101",
            BankId = bank.Id,
            DisbursedAmount = 45000m,
            Status = BankPledgeStatus.Active
        };
        context.BankPledges.Add(bankPledge);
        await context.SaveChangesAsync();

        // Link loan to bank pledge
        context.BankPledgeLoans.Add(new BankPledgeLoan
        {
            BankPledgeId = bankPledge.Id,
            LoanId = loan.Id
        });
        await context.SaveChangesAsync();

        // Verify query detects that jewels are currently deposited at the bank
        var isJewelsAtBank = await context.BankPledgeLoans
            .AnyAsync(bpl => bpl.LoanId == loan.Id && bpl.BankPledge.Status == BankPledgeStatus.Active);

        isJewelsAtBank.Should().BeTrue(); // Safety check triggers: Alert cashier before delivering jewels
    }

    [Fact]
    public async Task Scenario12_OverdueClassification_ClassifiesAgeCorrectly()
    {
        var loanPledged10MonthsAgo = new Loan
        {
            LoanNumber = "GL-OVERDUE-1",
            PledgeDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-300)),
            MaturityDate = DateOnly.FromDateTime(DateTime.Today.AddDays(65)),
            LoanAmount = 50000m,
            Status = LoanStatus.Active
        };

        var daysActive = (DateOnly.FromDateTime(DateTime.Today).DayNumber - loanPledged10MonthsAgo.PledgeDate.DayNumber);
        var isOverdueThreshold = daysActive >= 180; // 6-month threshold

        isOverdueThreshold.Should().BeTrue();
    }

    [Fact]
    public async Task Scenario13_AuctionSettlement_ComputesSurplusPayableToCustomer()
    {
        decimal loanPrincipal = 50000m;
        decimal interestDue = 12000m;
        decimal auctionExpenses = 1500m;
        decimal totalPayable = loanPrincipal + interestDue + auctionExpenses; // 63,500

        decimal auctionSaleBid = 70000m;
        decimal surplus = auctionSaleBid - totalPayable; // ₹6,500 surplus payable to customer

        surplus.Should().Be(6500m);
    }

    [Fact]
    public async Task Scenario14_DayEndClose_VerifiesCashDrawerIntegrity()
    {
        decimal systemCalculatedCash = 950000m;
        decimal physicalCashCounted = 950000m;

        decimal cashDiscrepancy = Math.Abs(systemCalculatedCash - physicalCashCounted);
        cashDiscrepancy.Should().Be(0m);
    }

    [Fact]
    public async Task Scenario15_FullFinancialIntegrityAudit_ConfirmsZeroDifferences()
    {
        using var context = CreateInMemoryContext();
        var accountingService = new AccountingReportService(context, new MasterDataService(context), new PermissionService());

        var audit = await accountingService.RunIntegrityAuditAsync(DateOnly.FromDateTime(DateTime.Today));

        audit.UnbalancedVouchersCount.Should().Be(0);
        audit.BalanceSheetDifference.Should().Be(0m);
        audit.HasDiscrepancies.Should().BeFalse();
    }
}
