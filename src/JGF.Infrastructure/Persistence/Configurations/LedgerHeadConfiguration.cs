using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="LedgerHead"/> entity.
/// Chart of accounts — each head represents a ledger in double-entry accounting.
/// </summary>
public class LedgerHeadConfiguration : IEntityTypeConfiguration<LedgerHead>
{
    public void Configure(EntityTypeBuilder<LedgerHead> builder)
    {
        builder.ToTable("LedgerHeads");

        builder.HasKey(lh => lh.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(lh => lh.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(lh => lh.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(lh => lh.Description)
            .HasMaxLength(500);

        builder.Property(lh => lh.AccountGroup)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(lh => lh.NormalBalance)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(lh => lh.IsSystemAccount)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(lh => lh.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(lh => lh.ParentLedgerHeadId);

        builder.Property(lh => lh.OpeningBalance)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(lh => lh.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(lh => lh.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(lh => lh.Code)
            .IsUnique()
            .HasDatabaseName("IX_LedgerHeads_Code");

        builder.HasIndex(lh => lh.AccountGroup)
            .HasDatabaseName("IX_LedgerHeads_AccountGroup");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(lh => lh.ParentLedgerHead)
            .WithMany(lh => lh.ChildLedgerHeads)
            .HasForeignKey(lh => lh.ParentLedgerHeadId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
