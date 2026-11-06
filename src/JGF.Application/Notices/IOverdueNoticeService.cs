using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Notices;

public enum OverdueBucket
{
    Normal,
    SixMonthReminder,
    TwelveMonthMatured,
    OverdueCritical,
    AuctionEligible
}

public record OverdueLoanSummaryDto(
    Guid LoanId,
    string LoanNumber,
    string CustomerName,
    string CustomerMobile,
    DateOnly PledgeDate,
    DateOnly? LastPaymentDate,
    int ElapsedMonths,
    decimal PrincipalBalance,
    decimal AccruedInterest,
    decimal TotalOutstanding,
    decimal CurrentGoldValue,
    decimal CurrentLtvRatio,
    OverdueBucket Bucket,
    int NoticesSentCount,
    DateOnly? LastNoticeDate,
    bool IsAuctionEligible
);

public record CreateNoticeBatchDto(
    List<Guid> LoanIds,
    NoticeType NoticeType,
    DateOnly NoticeDate,
    DateOnly DueDate,
    NoticeSentVia DeliveryMode,
    string? Remarks,
    Guid UserId,
    UserRole UserRole
);

public record ConductAuctionDto(
    Guid LoanId,
    DateOnly AuctionDate,
    string AuctionVenue,
    decimal ReservePrice,
    decimal SalePrice,
    decimal AuctionExpenses,
    string BuyerName,
    string? BuyerAddress,
    string? Remarks,
    Guid UserId,
    UserRole UserRole
);

public record AuctionSettlementResultDto(
    Guid AuctionId,
    string LoanNumber,
    decimal SalePrice,
    decimal TotalOutstanding,
    decimal AuctionExpenses,
    decimal SurplusAmount,
    decimal DeficitAmount,
    string VoucherNumber
);

public interface IOverdueNoticeService
{
    Task<List<OverdueLoanSummaryDto>> GetOverdueLoansAsync(CancellationToken ct = default);
    Task<List<Notice>> GetNoticesForLoanAsync(Guid loanId, CancellationToken ct = default);
    Task<List<Notice>> IssueNoticeBatchAsync(CreateNoticeBatchDto dto, CancellationToken ct = default);
    Task<string> GenerateNoticeHtmlOrTextAsync(Guid loanId, NoticeType noticeType, DateOnly noticeDate, DateOnly dueDate, bool inTamil = true, CancellationToken ct = default);
    Task<Auction> ApproveAndScheduleAuctionAsync(Guid loanId, DateOnly auctionDate, decimal reservePrice, Guid ownerUserId, UserRole userRole, CancellationToken ct = default);
    Task<AuctionSettlementResultDto> ConductAndSettleAuctionAsync(ConductAuctionDto dto, CancellationToken ct = default);
    Task<List<Auction>> GetAuctionRegisterAsync(CancellationToken ct = default);
}

