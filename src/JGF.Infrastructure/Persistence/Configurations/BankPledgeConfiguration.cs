using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="BankPledge"/> entity.
/// Represents bulk pledge headers for bank refinancing / re-pledge of gold loans.
/// </summary>
public class BankPledgeConfiguration : IEntityTypeConfiguration<BankPledge>
{
    public void Configure(EntityTypeBuilder<BankPledge> builder)
    {
        builder.ToTable("BankPledges");

        builder.HasKey(bp => bp.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(bp => bp.PledgeNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(bp => bp.PledgeDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(bp => bp.TotalPledgeAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(bp => bp.DisbursedAmount)
            .HasPrecision(18, 2);

        builder.Property(bp => bp.InterestRate)
            .HasPrecision(18, 4);

        builder.Property(bp => bp.MaturityDate)
            .HasColumnType("date");

        builder.Property(bp => bp.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(bp => bp.Remarks)
            .HasMaxLength(1000);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(bp => bp.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(bp => bp.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(bp => bp.PledgeNumber)
            .IsUnique()
            .HasDatabaseName("IX_BankPledges_PledgeNumber");

        builder.HasIndex(bp => bp.BankId)
            .HasDatabaseName("IX_BankPledges_BankId");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(bp => bp.Bank)
            .WithMany(b => b.BankPledges)
            .HasForeignKey(bp => bp.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(bp => bp.BankPledgeLoans)
            .WithOne(bpl => bpl.BankPledge)
            .HasForeignKey(bpl => bpl.BankPledgeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
