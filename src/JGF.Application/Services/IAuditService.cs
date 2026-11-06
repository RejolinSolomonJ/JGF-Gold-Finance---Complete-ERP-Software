using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Services;

public interface IAuditService
{
    Task<List<AuditLog>> QueryAuditLogsAsync(
        string? tableName = null,
        Guid? userId = null,
        DateTime? fromDateUtc = null,
        DateTime? toDateUtc = null,
        AuditAction? action = null,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default);
}

