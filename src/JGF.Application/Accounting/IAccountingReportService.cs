using JGF.Domain.Enums;

namespace JGF.Application.Accounting;

public record DayBookItemDto(
    Guid VoucherId,
    string VoucherNumber,
    DateOnly VoucherDate,
    string LedgerHeadCode,
    string LedgerHeadName,
    LedgerHeadType AccountGroup,
    decimal ReceiptAmount,
    decimal PaymentAmount,
    string Narration,
    string? ReferenceType,
    Guid? ReferenceId
);

public record DayBookSummaryDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningCash,
    decimal TotalReceipts,
    decimal TotalPayments,
    decimal ClosingCash,
    List<DayBookItemDto> Items
);

public record TrialBalanceRowDto(
    string Code,
    string Name,
    LedgerHeadType AccountGroup,
    decimal DebitBalance,
    decimal CreditBalance
);

public record TrialBalanceReportDto(
    DateOnly AsOfDate,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    List<TrialBalanceRowDto> Rows
);

public record IncomeExpenseRowDto(
    string Code,
    string Name,
    decimal Amount
);

public record ProfitAndLossReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    List<IncomeExpenseRowDto> IncomeRows,
    decimal TotalIncome,
    List<IncomeExpenseRowDto> ExpenseRows,
    decimal TotalExpense,
    decimal NetProfitOrLoss
);

public record BalanceSheetItemDto(
    string Code,
    string Name,
    decimal Amount
);

public record BalanceSheetReportDto(
    DateOnly AsOfDate,
    List<BalanceSheetItemDto> Assets,
    decimal TotalAssets,
    List<BalanceSheetItemDto> Liabilities,
    decimal TotalLiabilities,
    List<BalanceSheetItemDto> Capital,
    decimal TotalCapital,
    decimal CurrentYearNetProfit,
    decimal TotalLiabilitiesAndCapital,
    decimal Difference
);

public record AccountingIntegrityCheckDto(
    bool HasDiscrepancies,
    int UnbalancedVouchersCount,
    List<string> UnbalancedVoucherDetails,
    decimal BalanceSheetDifference,
    decimal CashBookClosingCash,
    decimal BalanceSheetCashAsset,
    decimal CashReconciliationDifference,
    decimal LoanModulePrincipalOutstanding,
    decimal JewelLoanLedgerHeadBalance,
    decimal LoanModuleVsLedgerDifference,
    decimal BankRepledgeModuleOutstanding,
    decimal BankRepledgeLedgerHeadBalance,
    decimal BankRepledgeVsLedgerDifference
);

public record CreateManualVoucherDto(
    DateOnly VoucherDate,
    VoucherType VoucherType,
    Guid LedgerHeadId,
    decimal Amount,
    string Narration,
    Guid UserId,
    UserRole UserRole
);

public interface IAccountingReportService
{
    Task<DayBookSummaryDto> GetDayBookAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task<ProfitAndLossReportDto> GetProfitAndLossAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<BalanceSheetReportDto> GetBalanceSheetAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task<AccountingIntegrityCheckDto> RunIntegrityAuditAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task<string> PostManualVoucherAsync(CreateManualVoucherDto dto, CancellationToken ct = default);
}
