using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Persistence;

/// <summary>
/// A disposable scope wrapper around JgfDbContext that supports either short-lived
/// instances from IDbContextFactory (which get disposed) or test-provided instances (which do not get disposed).
/// </summary>
public sealed class DbContextScope : IAsyncDisposable, IDisposable
{
    private readonly JgfDbContext _context;
    private readonly bool _shouldDispose;

    public DbContextScope(JgfDbContext context, bool shouldDispose)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _shouldDispose = shouldDispose;
    }

    public JgfDbContext Context => _context;

    public void Dispose()
    {
        if (_shouldDispose)
        {
            _context.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_shouldDispose)
        {
            await _context.DisposeAsync();
        }
    }
}
