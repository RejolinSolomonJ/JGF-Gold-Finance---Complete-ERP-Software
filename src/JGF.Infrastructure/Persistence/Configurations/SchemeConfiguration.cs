using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Scheme"/> entity.
/// Gold-loan product templates defining rates, tenures, and LTV.
/// </summary>
public class SchemeConfiguration : IEntityTypeConfiguration<Scheme>
{
    public void Configure(EntityTypeBuilder<Scheme> builder)
    {
        builder.ToTable("Schemes");

        builder.HasKey(s => s.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.InterestRate)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(s => s.PenalRate)
            .HasPrecision(18, 4);

        builder.Property(s => s.MinLoanAmount)
            .HasPrecision(18, 2);

        builder.Property(s => s.MaxLoanAmount)
            .HasPrecision(18, 2);

        builder.Property(s => s.MaxLtvPercentage)
            .HasPrecision(5, 2);

        builder.Property(s => s.TenureMonths)
            .IsRequired();

        builder.Property(s => s.GracePeriodDays);

        builder.Property(s => s.InterestCalculation)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(s => s.Code)
            .IsUnique()
            .HasDatabaseName("IX_Schemes_Code");

        // ── Navigation ────────────────────────────────────────────
        builder.HasMany(s => s.SchemeSlabs)
            .WithOne(ss => ss.Scheme)
            .HasForeignKey(ss => ss.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Loans)
            .WithOne(l => l.Scheme)
            .HasForeignKey(l => l.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
