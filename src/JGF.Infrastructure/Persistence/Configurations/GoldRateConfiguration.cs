using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="GoldRate"/> entity.
/// Stores daily gold market rate per gram for valuation.
/// </summary>
public class GoldRateConfiguration : IEntityTypeConfiguration<GoldRate>
{
    public void Configure(EntityTypeBuilder<GoldRate> builder)
    {
        builder.ToTable("GoldRates");

        builder.HasKey(gr => gr.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(gr => gr.Date)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(gr => gr.RatePerGram)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(gr => gr.Rate22K)
            .HasPrecision(18, 2);

        builder.Property(gr => gr.Rate18K)
            .HasPrecision(18, 2);

        builder.Property(gr => gr.Source)
            .HasMaxLength(200);

        builder.Property(gr => gr.Remarks)
            .HasMaxLength(500);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(gr => gr.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(gr => gr.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(gr => gr.Date)
            .IsUnique()
            .HasDatabaseName("IX_GoldRates_Date");
    }
}
