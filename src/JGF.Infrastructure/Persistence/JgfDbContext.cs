using JGF.Domain.Common;
using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JGF.Infrastructure.Persistence;

/// <summary>
/// Main EF Core DbContext for the JGF Gold-Loan Finance application.
/// Applies global query filters for soft delete, auto-sets audit timestamps,
/// and configures all entity mappings via IEntityTypeConfiguration classes.
/// </summary>
public class JgfDbContext : DbContext
{
    // ────────────────────────────── Core Entities ──────────────────────────────

    /// <summary>Customers / borrowers.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Gold loan accounts.</summary>
    public DbSet<Loan> Loans => Set<Loan>();

    /// <summary>Individual pledged jewel items on a loan.</summary>
    public DbSet<LoanItem> LoanItems => Set<LoanItem>();

    /// <summary>Receipts (payments received against loans).</summary>
    public DbSet<Receipt> Receipts => Set<Receipt>();

    /// <summary>Notices sent to borrowers (reminders, auction notices).</summary>
    public DbSet<Notice> Notices => Set<Notice>();

    /// <summary>Auctions and recovery records.</summary>
    public DbSet<Auction> Auctions => Set<Auction>();

    // ────────────────────────────── Configuration ─────────────────────────────

    /// <summary>Gold-loan schemes / product templates.</summary>
    public DbSet<Scheme> Schemes => Set<Scheme>();

    /// <summary>Interest slabs within a scheme.</summary>
    public DbSet<SchemeSlab> SchemeSlabs => Set<SchemeSlab>();

    /// <summary>Jewel type master (Ring, Chain, Bangle, etc.).</summary>
    public DbSet<JewelType> JewelTypes => Set<JewelType>();

    /// <summary>Daily gold rate.</summary>
    public DbSet<GoldRate> GoldRates => Set<GoldRate>();

    /// <summary>Day-close / day-end records.</summary>
    public DbSet<DayClose> DayCloses => Set<DayClose>();

    // ────────────────────────────── Accounting ─────────────────────────────────

    /// <summary>Accounting vouchers (Journal, Receipt, Payment, Contra).</summary>
    public DbSet<Voucher> Vouchers => Set<Voucher>();

    /// <summary>Individual debit/credit lines within a voucher.</summary>
    public DbSet<VoucherLine> VoucherLines => Set<VoucherLine>();

    /// <summary>Chart of accounts / ledger heads.</summary>
    public DbSet<LedgerHead> LedgerHeads => Set<LedgerHead>();

    /// <summary>Loan-specific sub-ledger entries (interest accrual, etc.).</summary>
    public DbSet<LoanLedgerEntry> LoanLedgerEntries => Set<LoanLedgerEntry>();

    // ────────────────────────────── Bank / Pledge ─────────────────────────────

    /// <summary>Bank master (for fund sources / re-pledge).</summary>
    public DbSet<Bank> Banks => Set<Bank>();

    /// <summary>Bank pledge headers (bulk pledges to banks for refinance).</summary>
    public DbSet<BankPledge> BankPledges => Set<BankPledge>();

    /// <summary>Many-to-many link between BankPledge and Loan.</summary>
    public DbSet<BankPledgeLoan> BankPledgeLoans => Set<BankPledgeLoan>();

    // ────────────────────────────── System ─────────────────────────────────────

    /// <summary>Application users.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Application settings (key-value).</summary>
    public DbSet<Setting> Settings => Set<Setting>();

    /// <summary>Auto-number series configuration.</summary>
    public DbSet<NumberSeries> NumberSerieses => Set<NumberSeries>();

    /// <summary>Immutable audit trail.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>System backup logs.</summary>
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();

    // ──────────────────────────────────────────────────────────────────────────

    public JgfDbContext(DbContextOptions<JgfDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Applies all IEntityTypeConfiguration classes from this assembly and
    /// registers global query filters for soft-deletable entities.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all Fluent API configurations from Configurations/ folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JgfDbContext).Assembly);

        // Apply global query filter for every entity implementing ISoftDeletable
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(JgfDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, new object[] { modelBuilder });
            }
        }
    }

    /// <summary>
    /// Generic helper to apply the soft-delete query filter.
    /// </summary>
    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <summary>
    /// Overrides SaveChangesAsync to automatically manage audit fields
    /// and soft-delete state before persisting.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Overrides SaveChanges for synchronous callers. Same audit logic applies.
    /// </summary>
    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// Iterates tracked entities and sets CreatedAtUtc/UpdatedAtUtc timestamps,
    /// and converts hard deletes into soft deletes for ISoftDeletable entities.
    /// </summary>
    private void ApplyAuditFields()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    entry.Entity.UpdatedAtUtc = utcNow;
                    if (entry.Entity.Id == Guid.Empty)
                        entry.Entity.Id = Guid.NewGuid();
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utcNow;
                    // Prevent overwriting CreatedAtUtc on update
                    entry.Property(nameof(AuditableEntity.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedByUserId)).IsModified = false;
                    break;
            }
        }

        // Convert hard deletes to soft deletes for ISoftDeletable entities
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAtUtc = utcNow;
            }
        }
    }
}
