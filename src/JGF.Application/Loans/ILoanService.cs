using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Loans;

public record CreateLoanItemDto(
    Guid JewelTypeId,
    string Description,
    int Quantity,
    decimal GrossWeight,
    decimal DeductionWeight,
    decimal NetWeight,
    decimal PurityPercent,
    decimal RatePerGram,
    decimal AppraisedValue
);

public record CreateLoanDto(
    Guid CustomerId,
    Guid SchemeId,
    DateOnly PledgeDate,
    decimal LoanAmount,
    List<CreateLoanItemDto> Items,
    string? NomineeName,
    string? NomineeRelationship,
    string? PacketNumber,
    string? JewelPhotoPath,
    string? OwnerOverrideReason,
    Guid UserId,
    UserRole UserRole
);

public record TopUpLoanDto(
    Guid LoanId,
    decimal TopUpAmount,
    string? Reason,
    Guid UserId,
    UserRole UserRole
);

public record RenewalDto(
    Guid ExistingLoanId,
    Guid NewSchemeId,
    DateOnly RenewalDate,
    decimal InterestPaidAmount,
    decimal? AdjustedPrincipal,
    Guid UserId,
    UserRole UserRole
);

public interface ILoanService
{
    Task<Loan?> GetLoanByIdAsync(Guid loanId, CancellationToken ct = default);
    Task<Loan?> FindLoanByNumberOrBarcodeAsync(string query, CancellationToken ct = default);
    Task<List<Loan>> SearchLoansAsync(string query, int take = 50, CancellationToken ct = default);
    Task<Loan> IssueNewLoanAsync(CreateLoanDto dto, CancellationToken ct = default);
    Task<Loan> TopUpLoanAsync(TopUpLoanDto dto, CancellationToken ct = default);
    Task<(Receipt ClosingReceipt, Loan NewLoan)> RenewLoanAsync(RenewalDto dto, CancellationToken ct = default);
    Task<List<LoanLedgerEntry>> GetLoanHistoryAsync(Guid loanId, CancellationToken ct = default);
}
