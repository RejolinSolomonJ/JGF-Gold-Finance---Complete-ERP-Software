using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Receipts;

public record CreateReceiptDto(
    Guid LoanId,
    DateOnly ReceiptDate,
    ReceiptType ReceiptType,
    PaymentMode PaymentMode,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal PenalInterestAmount,
    decimal PostalCharges,
    decimal OtherCharges,
    string? OtherChargesDescription,
    decimal AdjustmentAmount,
    string? AdjustmentReason,
    string? ReferenceNumber,
    DateOnly InterestFromDate,
    DateOnly InterestToDate,
    int DaysCharged,
    Guid UserId,
    UserRole UserRole
);

public interface IReceiptService
{
    Task<Receipt> ProcessReceiptAsync(CreateReceiptDto dto, CancellationToken ct = default);
    Task<Receipt?> GetReceiptByIdAsync(Guid receiptId, CancellationToken ct = default);
    Task<List<Receipt>> GetReceiptsForLoanAsync(Guid loanId, CancellationToken ct = default);
    Task<byte[]> GenerateReceiptPdfAsync(Guid receiptId, bool isDuplicate = false, CancellationToken ct = default);
    Task<byte[]> GenerateReleaseSlipPdfAsync(Guid loanId, CancellationToken ct = default);
}
