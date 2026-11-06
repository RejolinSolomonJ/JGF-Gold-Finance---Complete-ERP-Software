using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly JgfDbContext _context;

    public AuditService(JgfDbContext context)
    {
        _context = context;
    }

    public async Task<List<AuditLog>> QueryAuditLogsAsync(
        string? tableName = null,
        Guid? userId = null,
        DateTime? fromDateUtc = null,
        DateTime? toDateUtc = null,
        AuditAction? action = null,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(tableName))
            query = query.Where(a => a.TableName == tableName);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (fromDateUtc.HasValue)
            query = query.Where(a => a.Timestamp >= fromDateUtc.Value);

        if (toDateUtc.HasValue)
            query = query.Where(a => a.Timestamp <= toDateUtc.Value);

        if (action.HasValue)
            query = query.Where(a => a.Action == action.Value);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }
}
