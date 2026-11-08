using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

/// <summary>
/// Thread-safe base service providing short-lived DbContext instances via IDbContextFactory in production,
/// and backward-compatible direct context usage in unit tests.
/// </summary>
public abstract class DbServiceBase
{
    private readonly IDbContextFactory<JgfDbContext>? _contextFactory;
    private readonly JgfDbContext? _directContext;

    protected DbServiceBase(IDbContextFactory<JgfDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    protected DbServiceBase(JgfDbContext directContext)
    {
        _directContext = directContext ?? throw new ArgumentNullException(nameof(directContext));
    }

    protected async Task<DbContextScope> GetContextAsync(CancellationToken ct = default)
    {
        if (_contextFactory != null)
        {
            var ctx = await _contextFactory.CreateDbContextAsync(ct);
            return new DbContextScope(ctx, shouldDispose: true);
        }

        return new DbContextScope(_directContext!, shouldDispose: false);
    }

    protected DbContextScope GetContext()
    {
        if (_contextFactory != null)
        {
            var ctx = _contextFactory.CreateDbContext();
            return new DbContextScope(ctx, shouldDispose: true);
        }

        return new DbContextScope(_directContext!, shouldDispose: false);
    }
}

