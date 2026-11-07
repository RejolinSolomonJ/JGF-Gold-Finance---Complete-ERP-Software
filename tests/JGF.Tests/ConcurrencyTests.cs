using FluentAssertions;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JGF.Tests;

public class ConcurrencyTests
{
    private DbContextOptions<JgfDbContext> CreateOptions(string dbName)
    {
        return new DbContextOptionsBuilder<JgfDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
    }

    [Fact]
    public async Task SimultaneousUpdates_TriggerConcurrencyException_OnConflictingVersion()
    {
        var dbName = $"JGF_Concurrency_Test_{Guid.NewGuid()}";
        var options = CreateOptions(dbName);

        Guid loanId;

        // 1. Initial Loan Creation
        using (var setupContext = new JgfDbContext(options))
        {
            var customer = new Customer { Code = "C001", Name = "Shanmugam", Phone = "9876500000", Address = "Madurai" };
            var scheme = new Scheme { Code = "S1", Name = "Standard", InterestRatePerMonth = 1.5m };
            setupContext.Customers.Add(customer);
            setupContext.Schemes.Add(scheme);
            await setupContext.SaveChangesAsync();

            var loan = new Loan
            {
                LoanNumber = "GL-TEST-CONCURRENCY-1",
                CustomerId = customer.Id,
                SchemeId = scheme.Id,
                LoanAmount = 100000m,
                Status = LoanStatus.Active,
                ConcurrencyVersion = 1
            };
            setupContext.Loans.Add(loan);
            await setupContext.SaveChangesAsync();
            loanId = loan.Id;
        }

        // 2. Cashier 1 (PC1) loads the loan
        using var cashier1Context = new JgfDbContext(options);
        var loanCashier1 = await cashier1Context.Loans.FindAsync(loanId);
        loanCashier1.Should().NotBeNull();

        // 3. Cashier 2 (PC2) simultaneously loads the loan
        using var cashier2Context = new JgfDbContext(options);
        var loanCashier2 = await cashier2Context.Loans.FindAsync(loanId);
        loanCashier2.Should().NotBeNull();

        // 4. Cashier 1 updates and saves first (e.g. processes part payment)
        loanCashier1!.LoanAmount = 80000m;
        loanCashier1.ConcurrencyVersion++;
        await cashier1Context.SaveChangesAsync();

        // 5. Cashier 2 attempts to save conflicting changes with the old version (1)
        loanCashier2!.LoanAmount = 50000m;
        loanCashier2.ConcurrencyVersion = 1; // Cashier 2 is still working with old version 1

        Func<Task> act = async () =>
        {
            // Update entity in cashier2 context
            cashier2Context.Entry(loanCashier2).OriginalValues["ConcurrencyVersion"] = 1;
            await cashier2Context.SaveChangesAsync();
        };

        // Verifies optimistic concurrency prevents silent overwrites between PC1 and PC2
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}

