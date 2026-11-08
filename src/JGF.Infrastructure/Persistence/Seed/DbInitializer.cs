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

        if (!await context.SchemaVersions.AnyAsync())
        {
            var initialVersion = new SchemaVersion
            {
                AppVersion = "1.2.0",
                AppliedMigrations = "Initial_Create",
                AppliedAtUtc = DateTime.UtcNow,
                AppliedBy = "System",
                IsSuccess = true,
                ExecutionTimeMs = 0,
                PreUpdateBackupPath = "N/A (Initial Setup)",
                IntegrityCheckStatus = "Integrity Status: PASSED (Zero Discrepancies)",
                Notes = "Initial production database creation"
            };

            await context.SchemaVersions.AddAsync(initialVersion);
        }

        if (!await context.Users.AnyAsync())
        {
            var hasher = new JGF.Application.Security.BCryptPasswordHasher();
            var users = new List<User>
            {
                new()
                {
                    Username = "admin",
                    PasswordHash = hasher.HashPassword("admin"),
                    FullName = "Admin (Owner)",
                    Role = UserRole.Owner,
                    IsActive = true
                },
                new()
                {
                    Username = "manager",
                    PasswordHash = hasher.HashPassword("manager"),
                    FullName = "Manager S",
                    Role = UserRole.Manager,
                    IsActive = true
                },
                new()
                {
                    Username = "cashier",
                    PasswordHash = hasher.HashPassword("cashier"),
                    FullName = "Cashier K",
                    Role = UserRole.Cashier,
                    IsActive = true
                }
            };
            await context.Users.AddRangeAsync(users);
        }

        if (!await context.Settings.AnyAsync(s => s.Key == "Company.Name"))
        {
            var companySettings = new List<Setting>
            {
                new() { Key = "Company.Name", Value = "Jeba Gold Finance", Description = "Company Legal Name" },
                new() { Key = "Company.Address", Value = "123, Bazaar Street, Madurai - 625001", Description = "Shop Address" },
                new() { Key = "Company.Phone", Value = "0452-2345678", Description = "Contact Phone" },
                new() { Key = "Company.RegNo", Value = "TN-PBN-2024-001", Description = "Pawnbroker License Number" },
                new() { Key = "Company.GstNo", Value = "33AAAAA0000A1Z5", Description = "GSTIN" },
                new() { Key = "Company.LogoPath", Value = "Assets/logo.png", Description = "Company Logo File Path" },
                new() { Key = "Company.FinancialYear", Value = "2024-25", Description = "Current Financial Year" }
            };
            await context.Settings.AddRangeAsync(companySettings);
        }

        if (!await context.Schemes.AnyAsync())
        {
            var schemes = new List<Scheme>
            {
                new()
                {
                    Code = "S1",
                    Name = "Standard Gold Loan 12%",
                    InterestMethod = InterestMethod.Simple,
                    InterestRatePerMonth = 1.0m, // 12% p.a.
                    MinimumInterestDays = 7,
                    MinimumInterestMonths = 1,
                    MaxLtvPercent = 75.0m,
                    MaxTenureMonths = 12,
                    GraceDaysAfterExpiry = 7,
                    PenalInterestRatePerMonth = 0.5m
                },
                new()
                {
                    Code = "S2",
                    Name = "Slab Interest Scheme 12-18%",
                    InterestMethod = InterestMethod.Simple,
                    InterestRatePerMonth = 1.5m, // 18% p.a.
                    MinimumInterestDays = 15,
                    MinimumInterestMonths = 1,
                    MaxLtvPercent = 75.0m,
                    MaxTenureMonths = 12,
                    GraceDaysAfterExpiry = 7,
                    PenalInterestRatePerMonth = 0.5m
                }
            };
            await context.Schemes.AddRangeAsync(schemes);
        }

        if (!await context.GoldRates.AnyAsync())
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var goldRate = new GoldRate
            {
                Date = today,
                RatePerGram = 6850.00m,
                Rate22K = 6850.00m,
                Rate24K = 7450.00m,
                Rate18K = 5600.00m,
                Source = "Madurai Bullion Association",
                Remarks = "Standard Lending Rate 22K 916"
            };
            await context.GoldRates.AddAsync(goldRate);
        }

        if (!await context.Banks.AnyAsync())
        {
            var banks = new List<Bank>
            {
                new()
                {
                    Code = "SBI",
                    Name = "State Bank of India",
                    Branch = "Main Branch, Madurai",
                    AccountNumber = "10293847561",
                    IfscCode = "SBIN0000800",
                    ContactPerson = "R. Sundar (Chief Manager)",
                    ContactPhone = "0452-2531000",
                    InterestRate = 8.5m,
                    SanctionedLimit = 5000000m,
                    IsActive = true
                },
                new()
                {
                    Code = "CNB",
                    Name = "Canara Bank",
                    Branch = "Town Hall Road, Madurai",
                    AccountNumber = "98765432101",
                    IfscCode = "CNRB0001234",
                    ContactPerson = "K. Meenakshi",
                    ContactPhone = "0452-2348000",
                    InterestRate = 9.0m,
                    SanctionedLimit = 3000000m,
                    IsActive = true
                },
                new()
                {
                    Code = "MTH",
                    Name = "Muthoot Finance Ltd",
                    Branch = "Bazaar Branch, Madurai",
                    AccountNumber = "55443322110",
                    IfscCode = "MUTH0000101",
                    ContactPerson = "M. Suresh",
                    ContactPhone = "9840011223",
                    InterestRate = 10.5m,
                    SanctionedLimit = 10000000m,
                    IsActive = true
                }
            };
            await context.Banks.AddRangeAsync(banks);
        }

        if (!await context.Customers.AnyAsync())
        {
            var sampleCustomer = new Customer
            {
                Code = "C0001",
                Name = "Ramasamy M",
                FatherOrHusbandName = "Muthiah P",
                Phone = "9876543210",
                Gender = Gender.Male,
                Address = "45, North Veli Street, Madurai - 625001",
                City = "Madurai",
                Pincode = "625001",
                AadhaarNumber = "XXXXXXXX1234"
            };
            await context.Customers.AddAsync(sampleCustomer);
        }

        await context.SaveChangesAsync();
    }
}

