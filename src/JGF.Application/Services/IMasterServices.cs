using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Services;

public interface ICustomerService
{
    Task<List<Customer>> SearchCustomersAsync(string query, int take = 50, CancellationToken ct = default);
    Task<Customer?> GetCustomerByIdAsync(Guid id, CancellationToken ct = default);
    Task<Customer> CreateCustomerAsync(Customer customer, List<CustomerDocument>? documents = null, CancellationToken ct = default);
    Task<Customer> UpdateCustomerAsync(Customer customer, CancellationToken ct = default);
    Task<bool> SoftDeleteCustomerAsync(Guid customerId, CancellationToken ct = default);
}

public interface ISchemeService
{
    Task<List<Scheme>> GetAllSchemesAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<Scheme?> GetSchemeByIdAsync(Guid id, CancellationToken ct = default);
    Task<Scheme> CreateSchemeAsync(Scheme scheme, List<SchemeSlab>? slabs = null, CancellationToken ct = default);
    Task<Scheme> UpdateSchemeAsync(Scheme scheme, List<SchemeSlab>? slabs = null, CancellationToken ct = default);
    Task<bool> SoftDeleteSchemeAsync(Guid schemeId, CancellationToken ct = default);
}

public interface IJewelTypeService
{
    Task<List<JewelType>> GetAllJewelTypesAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<JewelType> CreateJewelTypeAsync(JewelType jewelType, CancellationToken ct = default);
    Task<JewelType> UpdateJewelTypeAsync(JewelType jewelType, CancellationToken ct = default);
    Task<bool> SoftDeleteJewelTypeAsync(Guid id, CancellationToken ct = default);
}

public interface IGoldRateService
{
    Task<GoldRate?> GetTodayRateAsync(CancellationToken ct = default);
    Task<List<GoldRate>> GetRecentRatesAsync(int days = 30, CancellationToken ct = default);
    Task<GoldRate> SetDailyRateAsync(GoldRate rate, CancellationToken ct = default);
}

public interface IBankService
{
    Task<List<Bank>> GetAllBanksAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<Bank> CreateBankAsync(Bank bank, CancellationToken ct = default);
    Task<Bank> UpdateBankAsync(Bank bank, CancellationToken ct = default);
    Task<bool> SoftDeleteBankAsync(Guid bankId, CancellationToken ct = default);
}

public interface IAccountingService
{
    Task<List<LedgerHead>> GetAllLedgerHeadsAsync(CancellationToken ct = default);
    Task<LedgerHead> CreateLedgerHeadAsync(LedgerHead ledgerHead, CancellationToken ct = default);
    Task<LedgerHead> UpdateLedgerHeadAsync(LedgerHead ledgerHead, CancellationToken ct = default);
    Task<bool> SoftDeleteLedgerHeadAsync(Guid ledgerHeadId, CancellationToken ct = default);
}

public interface INumberSeriesService
{
    Task<List<NumberSeries>> GetAllSeriesAsync(string? financialYear = null, CancellationToken ct = default);
    Task<string> GetNextNumberAsync(NumberSeriesType type, string financialYear, Guid? schemeId = null, CancellationToken ct = default);
    Task<NumberSeries> CreateOrUpdateSeriesAsync(NumberSeries series, CancellationToken ct = default);
}

