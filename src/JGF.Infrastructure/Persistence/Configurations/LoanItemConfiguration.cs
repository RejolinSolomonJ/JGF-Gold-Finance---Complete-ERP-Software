using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="LoanItem"/> entity.
/// Represents individual pledged jewellery items on a loan.
/// </summary>
public class LoanItemConfiguration : IEntityTypeConfiguration<LoanItem>
{
    public void Configure(EntityTypeBuilder<LoanItem> builder)
    {
        builder.ToTable("LoanItems");

        builder.HasKey(li => li.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(li => li.Description)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(li => li.Quantity)
            .IsRequired();

        builder.Property(li => li.GrossWeight)
            .IsRequired()
            .HasPrecision(10, 3);

        builder.Property(li => li.StoneWeight)
            .HasPrecision(10, 3);

        builder.Property(li => li.NetWeight)
            .IsRequired()
            .HasPrecision(10, 3);

        builder.Property(li => li.Purity)
            .HasMaxLength(10);

        builder.Property(li => li.PurityPercentage)
            .HasPrecision(5, 2);

        builder.Property(li => li.AppraisedValue)
            .HasPrecision(18, 2);

        builder.Property(li => li.MarketValue)
            .HasPrecision(18, 2);

        builder.Property(li => li.Remarks)
            .HasMaxLength(500);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(li => li.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(li => li.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(li => li.LoanId)
            .HasDatabaseName("IX_LoanItems_LoanId");

        builder.HasIndex(li => li.JewelTypeId)
            .HasDatabaseName("IX_LoanItems_JewelTypeId");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(li => li.Loan)
            .WithMany(l => l.LoanItems)
            .HasForeignKey(li => li.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(li => li.JewelType)
            .WithMany()
            .HasForeignKey(li => li.JewelTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
