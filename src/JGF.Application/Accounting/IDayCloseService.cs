using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Accounting;

public record DayCloseStatusDto(
    DateOnly Date,
    DayCloseStatus Status,
    decimal OpeningCash,
    decimal TotalCashIn,
    decimal TotalCashOut,
    decimal ClosingCash,
    int ActiveLoansCount,
    decimal TotalOutstandingPrincipal,
    DateTime? ClosedAtUtc,
    string? ClosedByUsername
);

public interface IDayCloseService
{
    Task<DayCloseStatusDto> GetDayStatusAsync(DateOnly date, CancellationToken ct = default);
    Task<DayClose> ExecuteDayEndCloseAsync(DateOnly date, decimal countedCash, string? remarks, Guid userId, UserRole userRole, CancellationToken ct = default);
    Task<DayClose> ReopenDayAsync(DateOnly date, string reopenReason, Guid userId, UserRole userRole, CancellationToken ct = default);
    Task<bool> IsDateLockedAsync(DateOnly date, CancellationToken ct = default);
}
