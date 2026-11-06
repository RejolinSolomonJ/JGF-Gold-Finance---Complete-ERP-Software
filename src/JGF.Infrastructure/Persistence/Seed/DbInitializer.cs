using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(JgfDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (!await context.LedgerHeads.AnyAsync())
        {
            var heads = new List<LedgerHead>
            {
                // Assets
                new() { Code = "1001", Name = "Cash on Hand", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
                new() { Code = "1002", Name = "Jewel Loan Receivable", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
                new() { Code = "1003", Name = "Bank Account (Primary)", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = true },
                new() { Code = "1004", Name = "Furniture & Fixtures", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = false },
                new() { Code = "1005", Name = "Locker & Safe", AccountGroup = LedgerHeadType.Asset, NormalBalance = "Debit", IsSystemAccount = false },

                // Liabilities
                new() { Code = "2001", Name = "Bank Repledge Loan Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit", IsSystemAccount = true },
                new() { Code = "2002", Name = "Auction Surplus Payable", AccountGroup = LedgerHeadType.Liability, NormalBalance = "Credit", IsSystemAccount = true },

                // Capital
                new() { Code = "3001", Name = "Owner's Capital", AccountGroup = LedgerHeadType.Capital, NormalBalance = "Credit", IsSystemAccount = true },

                // Income
                new() { Code = "4001", Name = "Jewel Loan Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit", IsSystemAccount = true },
                new() { Code = "4002", Name = "Penal Interest Income", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit", IsSystemAccount = true },
                new() { Code = "4003", Name = "Document & Processing Charges", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit", IsSystemAccount = false },
                new() { Code = "4004", Name = "Postal Charges Recovered", AccountGroup = LedgerHeadType.Income, NormalBalance = "Credit", IsSystemAccount = false },

                // Expense
                new() { Code = "5001", Name = "Bank Repledge Interest Expense", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit", IsSystemAccount = true },
                new() { Code = "5002", Name = "Shop Rent", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit", IsSystemAccount = false },
                new() { Code = "5003", Name = "Staff Salary", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit", IsSystemAccount = false },
                new() { Code = "5004", Name = "Postal & Courier Expenses", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit", IsSystemAccount = false },
                new() { Code = "5005", Name = "General Office Expenses", AccountGroup = LedgerHeadType.Expense, NormalBalance = "Debit", IsSystemAccount = false }
            };

            await context.LedgerHeads.AddRangeAsync(heads);
        }

        if (!await context.JewelTypes.AnyAsync())
        {
            var types = new List<JewelType>
            {
                new() { Code = "CH", Name = "Gold Chain", Description = "Gold Neck Chain", IsActive = true },
                new() { Code = "RG", Name = "Gold Ring", Description = "Finger Ring (Plain/Stone)", IsActive = true },
                new() { Code = "BG", Name = "Gold Bangle", Description = "Pair or Single Bangle", IsActive = true },
                new() { Code = "NK", Name = "Necklace", Description = "Traditional / Modern Gold Necklace", IsActive = true },
                new() { Code = "ER", Name = "Ear Studs / Jimikki", Description = "Ear Ornaments", IsActive = true },
                new() { Code = "CN", Name = "Gold Coin", Description = "Bullion / Purity Hallmarked Coin", IsActive = true }
            };

            await context.JewelTypes.AddRangeAsync(types);
        }

        if (!await context.NumberSerieses.AnyAsync())
        {
            var fy = "2024-25";
            var seriesList = new List<NumberSeries>
            {
                new() { SeriesType = NumberSeriesType.CustomerCode, Prefix = "C", FinancialYear = fy, LastNumber = 0, PadLength = 4, Separator = "" },
                new() { SeriesType = NumberSeriesType.ReceiptNumber, Prefix = "R", FinancialYear = fy, LastNumber = 0, PadLength = 5, Separator = "/" },
                new() { SeriesType = NumberSeriesType.VoucherNumber, Prefix = "V", FinancialYear = fy, LastNumber = 0, PadLength = 5, Separator = "/" }
            };

            await context.NumberSerieses.AddRangeAsync(seriesList);
        }

        await context.SaveChangesAsync();
    }
}

