using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="SchemeSlab"/> entity.
/// Interest rate slabs within a loan scheme (e.g., 0-50000 at 12%, 50001-100000 at 15%).
/// </summary>
public class SchemeSlabConfiguration : IEntityTypeConfiguration<SchemeSlab>
{
    public void Configure(EntityTypeBuilder<SchemeSlab> builder)
    {
        builder.ToTable("SchemeSlabs", t =>
        {
            // Check: MinAmount must be less than MaxAmount
            t.HasCheckConstraint("CK_SchemeSlabs_MinMax", "\"MinAmount\" < \"MaxAmount\"");
        });

        builder.HasKey(ss => ss.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(ss => ss.MinAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(ss => ss.MaxAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(ss => ss.InterestRate)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(ss => ss.PenalRate)
            .HasPrecision(18, 4);

        builder.Property(ss => ss.SlabOrder)
            .IsRequired();

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(ss => ss.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ss => ss.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(ss => ss.SchemeId)
            .HasDatabaseName("IX_SchemeSlabs_SchemeId");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(ss => ss.Scheme)
            .WithMany(s => s.SchemeSlabs)
            .HasForeignKey(ss => ss.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
