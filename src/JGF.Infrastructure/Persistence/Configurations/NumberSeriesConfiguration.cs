using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="NumberSeries"/> entity.
/// Auto-number series for generating sequential document numbers (loans, receipts, vouchers, etc.).
/// </summary>
public class NumberSeriesConfiguration : IEntityTypeConfiguration<NumberSeries>
{
    public void Configure(EntityTypeBuilder<NumberSeries> builder)
    {
        builder.ToTable("NumberSeries");

        builder.HasKey(ns => ns.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(ns => ns.SeriesType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ns => ns.Prefix)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(ns => ns.FinancialYear)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(ns => ns.LastNumber)
            .IsRequired();

        builder.Property(ns => ns.PadLength)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(ns => ns.Separator)
            .HasMaxLength(5)
            .HasDefaultValue("/");

        builder.Property(ns => ns.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(ns => ns.SchemeId);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(ns => ns.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ns => ns.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        // Composite unique: one series per type + prefix + financial year + optional scheme
        builder.HasIndex(ns => new { ns.SeriesType, ns.Prefix, ns.FinancialYear, ns.SchemeId })
            .IsUnique()
            .HasDatabaseName("IX_NumberSeries_Type_Prefix_FY_Scheme");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(ns => ns.Scheme)
            .WithMany()
            .HasForeignKey(ns => ns.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
