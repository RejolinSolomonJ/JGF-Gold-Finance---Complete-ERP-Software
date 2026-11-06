using JGF.Domain.Enums;

namespace JGF.Application.Reports;

public record JlReportFilterDto(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? SchemeId = null,
    Guid? CustomerId = null,
    LoanStatus? Status = null,
    string? SearchText = null,
    string? StorageLocation = null, // "All", "ShopLocker", "BankCustody"
    PaymentMode? PaymentMode = null
);

public record OutstandingLoanItemDto(
    Guid LoanId,
    string GlNumber,
    DateOnly PledgeDate,
    DateOnly MaturityDate,
    string CustomerName,
    string CustomerPhone,
    string SchemeName,
    decimal GrossWeight,
    decimal NetWeight,
    decimal LoanAmount,
    decimal PrincipalBalance,
    int DaysElapsed,
    decimal AccruedInterest,
    decimal TotalPayable,
    LoanStatus Status,
    bool IsOverdue,
    string? PacketNumber,
    string StorageLocation
);

public record OutstandingReportSummaryDto(
    int TotalLoans,
    decimal TotalGrossWeight,
    decimal TotalNetWeight,
    decimal TotalDisbursedPrincipal,
    decimal TotalOutstandingPrincipal,
    decimal TotalAccruedInterest,
    decimal TotalPayable,
    List<OutstandingLoanItemDto> Items
);

public record LoanSummaryItemDto(
    Guid SchemeId,
    string SchemeCode,
    string SchemeName,
    int TotalLoansCount,
    int ActiveLoansCount,
    int ClosedLoansCount,
    decimal TotalDisbursedAmount,
    decimal TotalPrincipalCollected,
    decimal TotalOutstandingPrincipal,
    decimal TotalGrossWeight,
    decimal TotalNetWeight,
    decimal TotalInterestCollected
);

public record LoanSummaryReportDto(
    int TotalSchemes,
    int GrandTotalLoans,
    int GrandTotalActive,
    int GrandTotalClosed,
    decimal GrandDisbursed,
    decimal GrandPrincipalCollected,
    decimal GrandOutstandingPrincipal,
    decimal GrandGrossWeight,
    decimal GrandNetWeight,
    decimal GrandInterestCollected,
    List<LoanSummaryItemDto> Items
);

public record PledgeReportItemDto(
    Guid LoanId,
    string GlNumber,
    DateOnly PledgeDate,
    DateOnly MaturityDate,
    string CustomerName,
    string CustomerPhone,
    string SchemeName,
    int ItemCount,
    decimal GrossWeight,
    decimal NetWeight,
    decimal LoanAmount,
    decimal MonthlyInterestRate,
    string? PacketNumber
);

public record PledgeReportSummaryDto(
    int TotalLoans,
    int TotalItems,
    decimal TotalGrossWeight,
    decimal TotalNetWeight,
    decimal TotalLoanAmount,
    List<PledgeReportItemDto> Items
);

public record ReleaseReportItemDto(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReleaseDate,
    string GlNumber,
    DateOnly OriginalPledgeDate,
    string CustomerName,
    string CustomerPhone,
    string SchemeName,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal PenalPaid,
    decimal OtherAndPostalCharges,
    decimal TotalAmountPaid,
    PaymentMode PaymentMode
);

public record ReleaseReportSummaryDto(
    int TotalClosedLoans,
    decimal TotalPrincipalPaid,
    decimal TotalInterestPaid,
    decimal TotalPenalPaid,
    decimal TotalOtherCharges,
    decimal GrandTotalAmountPaid,
    List<ReleaseReportItemDto> Items
);

public record JlCashReportItemDto(
    DateOnly Date,
    int LoansDisbursedCount,
    decimal LoansDisbursedAmount,
    int ReceiptsCount,
    decimal PrincipalCollectedAmount,
    decimal InterestCollectedAmount,
    decimal PenalCollectedAmount,
    decimal PostalChargesCollectedAmount,
    decimal OtherChargesCollectedAmount,
    decimal TotalInflow,
    decimal NetCashMovement
);

public record JlCashReportSummaryDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalDisbursedCount,
    decimal TotalDisbursedAmount,
    int TotalReceiptsCount,
    decimal TotalPrincipalCollected,
    decimal TotalInterestCollected,
    decimal TotalPenalCollected,
    decimal TotalPostalCollected,
    decimal GrandTotalInflow,
    decimal NetOverallCashMovement,
    List<JlCashReportItemDto> Items
);

public record AllStockReportItemDto(
    Guid JewelTypeId,
    string JewelTypeCode,
    string JewelTypeName,
    int TotalPieces,
    decimal TotalGrossWeight,
    decimal TotalDeductionWeight,
    decimal TotalNetWeight,
    decimal ShopLockerGrossWeight,
    decimal ShopLockerNetWeight,
    decimal BankCustodyGrossWeight,
    decimal BankCustodyNetWeight
);

public record AllStockReportSummaryDto(
    int TotalJewelCategories,
    int GrandTotalPieces,
    decimal GrandGrossWeight,
    decimal GrandDeductionWeight,
    decimal GrandNetWeight,
    decimal GrandShopLockerGrossWeight,
    decimal GrandShopLockerNetWeight,
    decimal GrandBankCustodyGrossWeight,
    decimal GrandBankCustodyNetWeight,
    List<AllStockReportItemDto> Items
);

public record JlStockItemDto(
    Guid LoanItemId,
    string GlNumber,
    DateOnly PledgeDate,
    string PacketNumber,
    string CustomerName,
    string Description,
    string? DescriptionTamil,
    string JewelTypeName,
    int Quantity,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    decimal PurityPercent,
    decimal AppraisedValue,
    string StorageLocation,
    LoanStatus LoanStatus
);

public record JlStockReportSummaryDto(
    int TotalItemsCount,
    int TotalPieces,
    decimal TotalGrossWeight,
    decimal TotalStoneWeight,
    decimal TotalNetWeight,
    decimal TotalAppraisedValue,
    List<JlStockItemDto> Items
);

public record RecordPostalChargeDto(
    Guid LoanId,
    decimal Amount,
    DateOnly ChargeDate,
    string Narration,
    PaymentMode PaymentMode,
    Guid UserId,
    UserRole UserRole
);

public record PostalChargeItemDto(
    Guid VoucherId,
    string VoucherNumber,
    DateOnly Date,
    string GlNumber,
    string CustomerName,
    string CustomerPhone,
    decimal Amount,
    string Narration,
    string PaymentMode
);

public record PostalChargeReportSummaryDto(
    int TotalEntries,
    decimal TotalPostalAmount,
    List<PostalChargeItemDto> Items
);

public record InterestCollectionItemDto(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    string GlNumber,
    string CustomerName,
    string SchemeName,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    int Months,
    int Days,
    decimal InterestAmount,
    decimal PenalInterestAmount,
    decimal TotalInterestCollected,
    PaymentMode PaymentMode
);

public record InterestCollectionReportSummaryDto(
    int TotalReceiptsCount,
    decimal TotalInterestAmount,
    decimal TotalPenalInterestAmount,
    decimal GrandTotalInterestCollected,
    List<InterestCollectionItemDto> Items
);

public record CustomerStatementTransactionDto(
    DateOnly Date,
    string TransactionType, // "Loan Disbursement", "Interest Payment", "Part-Payment", "Closure", "Postal Charge"
    string ReferenceNumber,
    string Particulars,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningPrincipalBalance
);

public record CustomerStatementReportDto(
    Guid CustomerId,
    string CustomerCode,
    string FullName,
    string? GuardianName,
    string MobileNumber,
    string? AlternateMobile,
    string? Address,
    string? City,
    int TotalLoansTaken,
    int ActiveLoansCount,
    int ClosedLoansCount,
    decimal CurrentOutstandingPrincipal,
    decimal CurrentAccruedInterest,
    decimal TotalPayableNow,
    List<CustomerStatementTransactionDto> Transactions
);

public interface IJlReportService
{
    Task<OutstandingReportSummaryDto> GetOutstandingReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<LoanSummaryReportDto> GetLoanSummaryReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<PledgeReportSummaryDto> GetPledgeReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<ReleaseReportSummaryDto> GetReleaseReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<JlCashReportSummaryDto> GetJlCashReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<AllStockReportSummaryDto> GetAllStockReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<JlStockReportSummaryDto> GetJlStockReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<string> RecordPostalChargeAsync(RecordPostalChargeDto dto, CancellationToken ct = default);
    Task<PostalChargeReportSummaryDto> GetPostalChargeReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<InterestCollectionReportSummaryDto> GetInterestCollectionReportAsync(JlReportFilterDto filter, CancellationToken ct = default);
    Task<CustomerStatementReportDto> GetCustomerStatementAsync(Guid customerId, CancellationToken ct = default);
}

