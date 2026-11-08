using JGF.Application.Accounting;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JGF.Infrastructure.Services;

public class DayCloseService : DbServiceBase, IDayCloseService
{
    private readonly IAccountingReportService _accountingReport;
    private readonly IBackupService _backupService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<DayCloseService> _logger;

    [ActivatorUtilitiesConstructor]
    public DayCloseService(
        IDbContextFactory<JgfDbContext> contextFactory,
        IAccountingReportService accountingReport,
        IBackupService backupService,
        IPermissionService permissionService,
        ILogger<DayCloseService> logger)
        : base(contextFactory)
    {
        _accountingReport = accountingReport;
        _backupService = backupService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public DayCloseService(
        JgfDbContext context,
        IAccountingReportService accountingReport,
        IBackupService backupService,
        IPermissionService permissionService,
        ILogger<DayCloseService> logger)
        : base(context)
    {
        _accountingReport = accountingReport;
        _backupService = backupService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<DayCloseStatusDto> GetDayStatusAsync(DateOnly date, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var existing = await context.DayCloses
            .AsNoTracking()
            .Include(d => d.ClosedByUser)
            .FirstOrDefaultAsync(d => d.CloseDate == date, ct);

        var db = await _accountingReport.GetDayBookAsync(date, date, ct);

        var activeLoansCount = await context.Loans
            .AsNoTracking()
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .CountAsync(ct);

        var activeLoansSum = await context.Loans
            .AsNoTracking()
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .SumAsync(l => l.LoanAmount, ct);

        return new DayCloseStatusDto(
            date,
            existing?.Status ?? DayCloseStatus.Open,
            db.OpeningCash,
            db.TotalReceipts,
            db.TotalPayments,
            db.ClosingCash,
            activeLoansCount,
            activeLoansSum,
            existing?.ClosedAtUtc,
            existing?.ClosedByUser?.FullName
        );
    }

    public async Task<DayClose> ExecuteDayEndCloseAsync(
        DateOnly date,
        decimal countedCash,
        string? remarks,
        Guid userId,
        UserRole userRole,
        CancellationToken ct = default)
    {
        _permissionService.EnsurePermission(userRole, Permission.CloseDay);

        var status = await GetDayStatusAsync(date, ct);
        if (status.Status == DayCloseStatus.Closed)
        {
            throw new InvalidOperationException($"Day {date:dd-MMM-yyyy} is already closed.");
        }

        var cashDiff = Math.Abs(status.ClosingCash - countedCash);
        if (cashDiff > 0)
        {
            _logger.LogWarning("Physical cash discrepancy on day close {Date}: System=₹{System}, Counted=₹{Counted}, Diff=₹{Diff}",
                date, status.ClosingCash, countedCash, cashDiff);
        }

        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var dayClose = await context.DayCloses.FirstOrDefaultAsync(d => d.CloseDate == date, ct);
        if (dayClose == null)
        {
            dayClose = new DayClose
            {
                CloseDate = date
            };
            await context.DayCloses.AddAsync(dayClose, ct);
        }

        dayClose.OpeningCash = status.OpeningCash;
        dayClose.TotalCashIn = status.TotalCashIn;
        dayClose.TotalCashOut = status.TotalCashOut;
        dayClose.ClosingCash = countedCash;
        dayClose.TotalLoansIssued = status.TotalCashOut;
        dayClose.TotalCollections = status.TotalCashIn;
        dayClose.TotalLoansActive = status.ActiveLoansCount;
        dayClose.TotalOutstandingPrincipal = status.TotalOutstandingPrincipal;
        dayClose.Status = DayCloseStatus.Closed;
        dayClose.ClosedByUserId = userId;
        dayClose.ClosedAtUtc = DateTime.UtcNow;
        dayClose.Remarks = remarks;

        await context.SaveChangesAsync(ct);

        // Auto-trigger daily encrypted backup upon successful close
        _ = Task.Run(async () =>
        {
            try
            {
                await _backupService.CreateBackupAsync("DayCloseDefaultMasterKey2026", BackupType.Scheduled, userId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup after day-end close failed.");
            }
        }, ct);

        return dayClose;
    }

    public async Task<DayClose> ReopenDayAsync(
        DateOnly date,
        string reopenReason,
        Guid userId,
        UserRole userRole,
        CancellationToken ct = default)
    {
        // Reopen is Owner ONLY
        if (userRole != UserRole.Owner)
        {
            throw new UnauthorizedAccessException("Reopening a closed day is restricted strictly to the Owner.");
        }

        if (string.IsNullOrWhiteSpace(reopenReason))
        {
            throw new ArgumentException("A reason must be stated to reopen a closed day.", nameof(reopenReason));
        }

        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var dayClose = await context.DayCloses.FirstOrDefaultAsync(d => d.CloseDate == date, ct)
            ?? throw new ArgumentException("Day close record not found.");

        dayClose.Status = DayCloseStatus.Reopened;
        dayClose.ReopenedByUserId = userId;
        dayClose.ReopenedAtUtc = DateTime.UtcNow;
        dayClose.Remarks += $" [Reopened: {reopenReason}]";

        // Audit log
        var audit = new AuditLog
        {
            TableName = "DayCloses",
            RecordId = dayClose.Id,
            Action = AuditAction.Update,
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            NewValues = $"{{\"Action\": \"DayReopened\", \"Date\": \"{date:yyyy-MM-dd}\", \"Reason\": \"{reopenReason}\"}}"
        };
        await context.AuditLogs.AddAsync(audit, ct);

        await context.SaveChangesAsync(ct);
        return dayClose;
    }

    public async Task<bool> IsDateLockedAsync(DateOnly date, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var dc = await scope.Context.DayCloses.AsNoTracking().FirstOrDefaultAsync(d => d.CloseDate == date, ct);
        return dc != null && dc.Status == DayCloseStatus.Closed;
    }
}
