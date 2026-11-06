using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class MasterDataService : 
    ICustomerService, 
    ISchemeService, 
    IJewelTypeService, 
    IGoldRateService, 
    IBankService, 
    IAccountingService, 
    INumberSeriesService
{
    private readonly JgfDbContext _context;

    public MasterDataService(JgfDbContext context)
    {
        _context = context;
    }

    // ── Customers ─────────────────────────────────────────────────────────────
    public async Task<List<Customer>> SearchCustomersAsync(string query, int take = 50, CancellationToken ct = default)
    {
        var q = query.Trim().ToLower();
        return await _context.Customers
            .Where(c => c.Code.ToLower().Contains(q) || 
                        c.FullName.ToLower().Contains(q) || 
                        c.MobileNumber.Contains(q) ||
                        (c.AadhaarNumber != null && c.AadhaarNumber.Contains(q)))
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<Customer?> GetCustomerByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer, List<CustomerDocument>? documents = null, CancellationToken ct = default)
    {
        await _context.Customers.AddAsync(customer, ct);
        if (documents != null && documents.Count > 0)
        {
            foreach (var doc in documents)
            {
                doc.CustomerId = customer.Id;
            }
            await _context.Set<CustomerDocument>().AddRangeAsync(documents, ct);
        }
        await _context.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<Customer> UpdateCustomerAsync(Customer customer, CancellationToken ct = default)
    {
        _context.Customers.Update(customer);
        await _context.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<bool> SoftDeleteCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, ct);
        if (customer == null) return false;
        _context.Customers.Remove(customer); // Converted to soft-delete by interceptor / SaveChanges
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Schemes ───────────────────────────────────────────────────────────────
    public async Task<List<Scheme>> GetAllSchemesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _context.Schemes.Include(s => s.Slabs).AsQueryable();
        if (activeOnly) query = query.Where(s => s.IsActive);
        return await query.ToListAsync(ct);
    }

    public async Task<Scheme?> GetSchemeByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Schemes.Include(s => s.Slabs).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Scheme> CreateSchemeAsync(Scheme scheme, List<SchemeSlab>? slabs = null, CancellationToken ct = default)
    {
        await _context.Schemes.AddAsync(scheme, ct);
        if (slabs != null && slabs.Count > 0)
        {
            foreach (var slab in slabs) slab.SchemeId = scheme.Id;
            await _context.SchemeSlabs.AddRangeAsync(slabs, ct);
        }
        await _context.SaveChangesAsync(ct);
        return scheme;
    }

    public async Task<Scheme> UpdateSchemeAsync(Scheme scheme, List<SchemeSlab>? slabs = null, CancellationToken ct = default)
    {
        _context.Schemes.Update(scheme);
        await _context.SaveChangesAsync(ct);
        return scheme;
    }

    public async Task<bool> SoftDeleteSchemeAsync(Guid schemeId, CancellationToken ct = default)
    {
        var scheme = await _context.Schemes.FindAsync(new object[] { schemeId }, ct);
        if (scheme == null) return false;
        _context.Schemes.Remove(scheme);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Jewel Types ───────────────────────────────────────────────────────────
    public async Task<List<JewelType>> GetAllJewelTypesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var q = _context.JewelTypes.AsQueryable();
        if (activeOnly) q = q.Where(j => j.IsActive);
        return await q.OrderBy(j => j.Name).ToListAsync(ct);
    }

    public async Task<JewelType> CreateJewelTypeAsync(JewelType jewelType, CancellationToken ct = default)
    {
        await _context.JewelTypes.AddAsync(jewelType, ct);
        await _context.SaveChangesAsync(ct);
        return jewelType;
    }

    public async Task<JewelType> UpdateJewelTypeAsync(JewelType jewelType, CancellationToken ct = default)
    {
        _context.JewelTypes.Update(jewelType);
        await _context.SaveChangesAsync(ct);
        return jewelType;
    }

    public async Task<bool> SoftDeleteJewelTypeAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.JewelTypes.FindAsync(new object[] { id }, ct);
        if (item == null) return false;
        _context.JewelTypes.Remove(item);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Gold Rates ────────────────────────────────────────────────────────────
    public async Task<GoldRate?> GetTodayRateAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await _context.GoldRates.FirstOrDefaultAsync(g => g.Date == today, ct);
    }

    public async Task<List<GoldRate>> GetRecentRatesAsync(int days = 30, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        return await _context.GoldRates
            .Where(g => g.Date >= cutoff)
            .OrderByDescending(g => g.Date)
            .ToListAsync(ct);
    }

    public async Task<GoldRate> SetDailyRateAsync(GoldRate rate, CancellationToken ct = default)
    {
        var existing = await _context.GoldRates.FirstOrDefaultAsync(g => g.Date == rate.Date, ct);
        if (existing != null)
        {
            existing.RatePerGram = rate.RatePerGram;
            existing.Rate22K = rate.Rate22K;
            existing.Rate18K = rate.Rate18K;
            existing.Remarks = rate.Remarks;
            _context.GoldRates.Update(existing);
            await _context.SaveChangesAsync(ct);
            return existing;
        }

        await _context.GoldRates.AddAsync(rate, ct);
        await _context.SaveChangesAsync(ct);
        return rate;
    }

    // ── Banks & NBFCs ─────────────────────────────────────────────────────────
    public async Task<List<Bank>> GetAllBanksAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var q = _context.Banks.AsQueryable();
        if (activeOnly) q = q.Where(b => b.IsActive);
        return await q.OrderBy(b => b.Name).ToListAsync(ct);
    }

    public async Task<Bank> CreateBankAsync(Bank bank, CancellationToken ct = default)
    {
        await _context.Banks.AddAsync(bank, ct);
        await _context.SaveChangesAsync(ct);
        return bank;
    }

    public async Task<Bank> UpdateBankAsync(Bank bank, CancellationToken ct = default)
    {
        _context.Banks.Update(bank);
        await _context.SaveChangesAsync(ct);
        return bank;
    }

    public async Task<bool> SoftDeleteBankAsync(Guid bankId, CancellationToken ct = default)
    {
        var bank = await _context.Banks.FindAsync(new object[] { bankId }, ct);
        if (bank == null) return false;
        _context.Banks.Remove(bank);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Ledger Heads ──────────────────────────────────────────────────────────
    public async Task<List<LedgerHead>> GetAllLedgerHeadsAsync(CancellationToken ct = default)
    {
        return await _context.LedgerHeads.OrderBy(l => l.Code).ToListAsync(ct);
    }

    public async Task<LedgerHead> CreateLedgerHeadAsync(LedgerHead ledgerHead, CancellationToken ct = default)
    {
        await _context.LedgerHeads.AddAsync(ledgerHead, ct);
        await _context.SaveChangesAsync(ct);
        return ledgerHead;
    }

    public async Task<LedgerHead> UpdateLedgerHeadAsync(LedgerHead ledgerHead, CancellationToken ct = default)
    {
        _context.LedgerHeads.Update(ledgerHead);
        await _context.SaveChangesAsync(ct);
        return ledgerHead;
    }

    public async Task<bool> SoftDeleteLedgerHeadAsync(Guid ledgerHeadId, CancellationToken ct = default)
    {
        var head = await _context.LedgerHeads.FindAsync(new object[] { ledgerHeadId }, ct);
        if (head == null || head.IsSystemAccount) return false;
        _context.LedgerHeads.Remove(head);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Number Series ─────────────────────────────────────────────────────────
    public async Task<List<NumberSeries>> GetAllSeriesAsync(string? financialYear = null, CancellationToken ct = default)
    {
        var q = _context.NumberSerieses.AsQueryable();
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(n => n.FinancialYear == financialYear);
        return await q.ToListAsync(ct);
    }

    public async Task<string> GetNextNumberAsync(NumberSeriesType type, string financialYear, Guid? schemeId = null, CancellationToken ct = default)
    {
        var series = await _context.NumberSerieses
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
            await _context.NumberSerieses.AddAsync(series, ct);
        }

        series.LastNumber += 1;
        await _context.SaveChangesAsync(ct);

        var numStr = series.LastNumber.ToString().PadLeft(series.PadLength, '0');
        return string.IsNullOrEmpty(series.Separator) 
            ? $"{series.Prefix}{numStr}" 
            : $"{series.Prefix}{series.Separator}{series.FinancialYear}{series.Separator}{numStr}";
    }

    public async Task<NumberSeries> CreateOrUpdateSeriesAsync(NumberSeries series, CancellationToken ct = default)
    {
        if (series.Id == Guid.Empty)
            await _context.NumberSerieses.AddAsync(series, ct);
        else
            _context.NumberSerieses.Update(series);

        await _context.SaveChangesAsync(ct);
        return series;
    }
}
