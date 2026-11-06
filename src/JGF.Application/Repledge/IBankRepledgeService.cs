using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Repledge;

public record CreateBankPledgeDto(
    Guid BankId,
    string PledgeNumber,
    DateOnly PledgeDate,
    DateOnly? MaturityDate,
    decimal DisbursedAmount,
    decimal InterestRate,
    decimal BankWeight,
    string? BankAccountOrLoanNumber,
    string? AccountHolderName,
    string? BankReceiptNumber,
    List<Guid> LoanIdsToPledge,
    string? Remarks,
    Guid UserId,
    UserRole UserRole
);

public record SettleBankPaymentDto(
    Guid BankPledgeId,
    DateOnly PaymentDate,
    BankPaymentType PaymentType,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal PostalCharges,
    List<Guid>? ReleasedLoanIds,
    string? ReferenceNumber,
    string? Remarks,
    Guid UserId,
    UserRole UserRole
);

public record BankPledgeSummaryDto(
    Guid Id,
    string BankCode,
    string BankName,
    string PledgeNumber,
    string? BankAccountOrLoanNumber,
    DateOnly PledgeDate,
    DateOnly? MaturityDate,
    decimal TotalCustomerLoanAmount,
    decimal DisbursedAmount,
    decimal InterestRate,
    int TotalPledgedLoansCount,
    int ActivePledgedLoansCount,
    BankPledgeStatus Status,
    bool IsOverdue,
    bool IsDueSoon // Within 15 days
);

public record PawnVsBankDifferenceDto(
    string BankName,
    string BankPledgeNumber,
    string CustomerLoanNumber,
    string CustomerName,
    decimal CustomerNetWeight,
    decimal CustomerLoanAmount,
    decimal BankApportionedAmount,
    decimal Difference
);

public record JewelCustodyStockDto(
    string CustodyLocation, // "In Shop Locker" vs "In Bank Custody (Bank Name)"
    string LoanNumber,
    string CustomerName,
    string JewelDescription,
    int Quantity,
    decimal GrossWeight,
    decimal NetWeight,
    decimal AppraisedValue,
    string PacketNumber
);

public interface IBankRepledgeService
{
    Task<List<BankPledgeSummaryDto>> GetAllBankPledgesAsync(CancellationToken ct = default);
    Task<BankPledge?> GetBankPledgeDetailsAsync(Guid bankPledgeId, CancellationToken ct = default);
    Task<List<Loan>> GetAvailableLoansForRepledgeAsync(CancellationToken ct = default);
    Task<BankPledge> CreateBankPledgeAsync(CreateBankPledgeDto dto, CancellationToken ct = default);
    Task<(BankPayment Payment, string RzReceiptNumber)> ProcessBankPaymentAsync(SettleBankPaymentDto dto, CancellationToken ct = default);
    Task<bool> IsLoanInBankCustodyAsync(Guid loanId, CancellationToken ct = default);
    Task<List<PawnVsBankDifferenceDto>> GetPawnVsBankDifferenceReportAsync(CancellationToken ct = default);
    Task<List<JewelCustodyStockDto>> GetJewelStockCustodyReportAsync(CancellationToken ct = default);
}
