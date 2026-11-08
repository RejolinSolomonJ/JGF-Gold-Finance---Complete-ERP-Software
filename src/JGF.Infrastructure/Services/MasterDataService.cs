using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JGF.Infrastructure.Services;

public class MasterDataService : 
    DbServiceBase,
    ICustomerService, 
    ISchemeService, 
    IJewelTypeService, 
    IGoldRateService, 
    IBankService, 
    IAccountingService, 
    INumberSeriesService
{
    [ActivatorUtilitiesConstructor]
    public MasterDataService(IDbContextFactory<JgfDbContext> contextFactory)
        : base(contextFactory)
    {
    }

    public MasterDataService(JgfDbContext context)
        : base(context)
    {
    }

    // ── Customers ─────────────────────────────────────────────────────────────
    public async Task<List<Customer>> SearchCustomersAsync(string query, int take = 50, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var q = query.Trim().ToLower();
        var dbQuery = context.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            dbQuery = dbQuery.Where(c => c.Code.ToLower().Contains(q) || 
                                         c.Name.ToLower().Contains(q) || 
                                         (c.NameTamil != null && c.NameTamil.Contains(q)) ||
                                         c.Phone.Contains(q) ||
                                         (c.AadhaarNumber != null && c.AadhaarNumber.Contains(q)));
        }

        return await dbQuery
            .OrderBy(c => c.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<Customer?> GetCustomerByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer, List<CustomerDocument>? documents = null, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        await context.Customers.AddAsync(customer, ct);
        if (documents != null && documents.Count > 0)
        {
            foreach (var doc in documents)
            {
                doc.CustomerId = customer.Id;
            }
            await context.Set<CustomerDocument>().AddRangeAsync(documents, ct);
        }
        await context.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<Customer> UpdateCustomerAsync(Customer customer, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        context.Customers.Update(customer);
        await context.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<bool> SoftDeleteCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var customer = await context.Customers.FindAsync(new object[] { customerId }, ct);
        if (customer == null) return false;
        context.Customers.Remove(customer); // Converted to soft-delete by interceptor / SaveChanges
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ── Schemes ───────────────────────────────────────────────────────────────
    public async Task<List<Scheme>> GetAllSchemesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var query = scope.Context.Schemes.AsNoTracking().Include(s => s.Slabs).AsQueryable();
        if (activeOnly) query = query.Where(s => s.IsActive);
        return await query.ToListAsync(ct);
    }

    public async Task<Scheme?> GetSchemeByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.Schemes.AsNoTracking().Include(s => s.Slabs).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Scheme> CreateSchemeAsync(Scheme scheme, List<SchemeSlab>? slabs = null, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        await context.Schemes.AddAsync(scheme, ct);
        if (slabs != null && slabs.Count > 0)
        {
            foreach (var slab in slabs) slab.SchemeId = scheme.Id;
            await context.SchemeSlabs.AddRangeAsync(slabs, ct);
        }
        await context.SaveChangesAsync(ct);
        return scheme;
    }

    public async Task<Scheme> UpdateSchemeAsync(Scheme scheme, List<SchemeSlab>? slabs = null, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        context.Schemes.Update(scheme);
        await context.SaveChangesAsync(ct);
        return scheme;
    }

    public async Task<bool> SoftDeleteSchemeAsync(Guid schemeId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var scheme = await context.Schemes.FindAsync(new object[] { schemeId }, ct);
        if (scheme == null) return false;
        context.Schemes.Remove(scheme);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ── Jewel Types ───────────────────────────────────────────────────────────
    public async Task<List<JewelType>> GetAllJewelTypesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var q = scope.Context.JewelTypes.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(j => j.IsActive);
        return await q.OrderBy(j => j.Name).ToListAsync(ct);
    }

    public async Task<JewelType> CreateJewelTypeAsync(JewelType jewelType, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        await context.JewelTypes.AddAsync(jewelType, ct);
        await context.SaveChangesAsync(ct);
        return jewelType;
    }

    public async Task<JewelType> UpdateJewelTypeAsync(JewelType jewelType, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        context.JewelTypes.Update(jewelType);
        await context.SaveChangesAsync(ct);
        return jewelType;
    }

    public async Task<bool> SoftDeleteJewelTypeAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var item = await context.JewelTypes.FindAsync(new object[] { id }, ct);
        if (item == null) return false;
        context.JewelTypes.Remove(item);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ── Gold Rates ────────────────────────────────────────────────────────────
    public async Task<GoldRate?> GetTodayRateAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await scope.Context.GoldRates.AsNoTracking().FirstOrDefaultAsync(g => g.Date == today, ct);
    }

    public async Task<List<GoldRate>> GetRecentRatesAsync(int days = 30, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        return await scope.Context.GoldRates
            .AsNoTracking()
            .Where(g => g.Date >= cutoff)
            .OrderByDescending(g => g.Date)
            .ToListAsync(ct);
    }

    public async Task<GoldRate> SetDailyRateAsync(GoldRate rate, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var existing = await context.GoldRates.FirstOrDefaultAsync(g => g.Date == rate.Date, ct);
        if (existing != null)
        {
            existing.RatePerGram = rate.RatePerGram;
            existing.Rate22K = rate.Rate22K;
            existing.Rate18K = rate.Rate18K;
            existing.Remarks = rate.Remarks;
            context.GoldRates.Update(existing);
            await context.SaveChangesAsync(ct);
            return existing;
        }

        await context.GoldRates.AddAsync(rate, ct);
        await context.SaveChangesAsync(ct);
        return rate;
    }

    // ── Banks & NBFCs ─────────────────────────────────────────────────────────
    public async Task<List<Bank>> GetAllBanksAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var q = scope.Context.Banks.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(b => b.IsActive);
        return await q.OrderBy(b => b.Name).ToListAsync(ct);
    }

    public async Task<Bank> CreateBankAsync(Bank bank, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        await context.Banks.AddAsync(bank, ct);
        await context.SaveChangesAsync(ct);
        return bank;
    }

    public async Task<Bank> UpdateBankAsync(Bank bank, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        context.Banks.Update(bank);
        await context.SaveChangesAsync(ct);
        return bank;
    }

    public async Task<bool> SoftDeleteBankAsync(Guid bankId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var bank = await context.Banks.FindAsync(new object[] { bankId }, ct);
        if (bank == null) return false;
        context.Banks.Remove(bank);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ── Ledger Heads ──────────────────────────────────────────────────────────
    public async Task<List<LedgerHead>> GetAllLedgerHeadsAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.LedgerHeads.AsNoTracking().OrderBy(l => l.Code).ToListAsync(ct);
    }

    public async Task<LedgerHead> CreateLedgerHeadAsync(LedgerHead ledgerHead, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        await context.LedgerHeads.AddAsync(ledgerHead, ct);
        await context.SaveChangesAsync(ct);
        return ledgerHead;
    }

    public async Task<LedgerHead> UpdateLedgerHeadAsync(LedgerHead ledgerHead, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        context.LedgerHeads.Update(ledgerHead);
        await context.SaveChangesAsync(ct);
        return ledgerHead;
    }

    public async Task<bool> SoftDeleteLedgerHeadAsync(Guid ledgerHeadId, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var head = await context.LedgerHeads.FindAsync(new object[] { ledgerHeadId }, ct);
        if (head == null || head.IsSystemAccount) return false;
        context.LedgerHeads.Remove(head);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ── Number Series ─────────────────────────────────────────────────────────
    public async Task<List<NumberSeries>> GetAllSeriesAsync(string? financialYear = null, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var q = scope.Context.NumberSerieses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(n => n.FinancialYear == financialYear);
        return await q.ToListAsync(ct);
    }

    public async Task<string> GetNextNumberAsync(NumberSeriesType type, string financialYear, Guid? schemeId = null, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var series = await context.NumberSerieses
            .FirstOrDefaultAsync(n => n.SeriesType == type && n.FinancialYear == financialYear && n.SchemeId == schemeId, ct);

        if (series == null)
        {
            series = new NumberSeries
            {
                SeriesType = type,
                FinancialYear = financialYear,
                SchemeId = schemeId,
                Prefix = type switch
                {
                    NumberSeriesType.CustomerCode => "C",
                    NumberSeriesType.ReceiptNumber => "R",
                    NumberSeriesType.VoucherNumber => "V",
                    _ => "GL"
                },
                LastNumber = 0,
                PadLength = 4,
                Separator = "/"
            };
            await context.NumberSerieses.AddAsync(series, ct);
        }

        series.LastNumber += 1;
        await context.SaveChangesAsync(ct);

        var numStr = series.LastNumber.ToString().PadLeft(series.PadLength, '0');
        return string.IsNullOrEmpty(series.Separator) 
            ? $"{series.Prefix}{numStr}" 
            : $"{series.Prefix}{series.Separator}{series.FinancialYear}{series.Separator}{numStr}";
    }

    public async Task<NumberSeries> CreateOrUpdateSeriesAsync(NumberSeries series, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        if (series.Id == Guid.Empty)
            await context.NumberSerieses.AddAsync(series, ct);
        else
            context.NumberSerieses.Update(series);

        await context.SaveChangesAsync(ct);
        return series;
    }
}
