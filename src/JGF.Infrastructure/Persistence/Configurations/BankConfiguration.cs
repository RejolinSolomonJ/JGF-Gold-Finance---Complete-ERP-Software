using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Bank"/> entity.
/// Bank master for fund sources and re-pledge arrangements.
/// </summary>
public class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("Banks");

        builder.HasKey(b => b.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(b => b.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Branch)
            .HasMaxLength(200);

        builder.Property(b => b.AccountNumber)
            .HasMaxLength(50);

        builder.Property(b => b.IfscCode)
            .HasMaxLength(15);

        builder.Property(b => b.ContactPerson)
            .HasMaxLength(200);

        builder.Property(b => b.ContactPhone)
            .HasMaxLength(15);

        builder.Property(b => b.InterestRate)
            .HasPrecision(18, 4);

        builder.Property(b => b.SanctionedLimit)
            .HasPrecision(18, 2);

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(b => b.Remarks)
            .HasMaxLength(1000);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(b => b.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(b => b.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(b => b.Code)
            .IsUnique()
            .HasDatabaseName("IX_Banks_Code");

        // ── Navigation ────────────────────────────────────────────
        builder.HasMany(b => b.BankPledges)
            .WithOne(bp => bp.Bank)
            .HasForeignKey(bp => bp.BankId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
