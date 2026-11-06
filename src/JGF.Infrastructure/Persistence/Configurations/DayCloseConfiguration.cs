using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="DayClose"/> entity.
/// End-of-day closure records with summary balances and cash position.
/// </summary>
public class DayCloseConfiguration : IEntityTypeConfiguration<DayClose>
{
    public void Configure(EntityTypeBuilder<DayClose> builder)
    {
        builder.ToTable("DayCloses");

        builder.HasKey(dc => dc.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(dc => dc.CloseDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(dc => dc.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(dc => dc.OpeningCash)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(dc => dc.TotalCashIn)
            .HasPrecision(18, 2);

        builder.Property(dc => dc.TotalCashOut)
            .HasPrecision(18, 2);

        builder.Property(dc => dc.ClosingCash)
            .HasPrecision(18, 2);

        builder.Property(dc => dc.TotalLoansIssued)
            .HasPrecision(18, 2);

        builder.Property(dc => dc.TotalCollections)
            .HasPrecision(18, 2);

        builder.Property(dc => dc.LoansIssuedCount);
        builder.Property(dc => dc.LoansClosedCount);
        builder.Property(dc => dc.ReceiptsCount);

        builder.Property(dc => dc.Remarks)
            .HasMaxLength(1000);

        builder.Property(dc => dc.ClosedAtUtc);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(dc => dc.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(dc => dc.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(dc => dc.CloseDate)
            .IsUnique()
            .HasDatabaseName("IX_DayCloses_CloseDate");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(dc => dc.ClosedByUser)
            .WithMany()
            .HasForeignKey(dc => dc.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
