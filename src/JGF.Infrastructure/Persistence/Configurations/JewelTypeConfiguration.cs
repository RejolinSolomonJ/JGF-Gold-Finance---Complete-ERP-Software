using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="JewelType"/> entity.
/// Master table for types of jewellery (Ring, Chain, Bangle, Necklace, etc.).
/// </summary>
public class JewelTypeConfiguration : IEntityTypeConfiguration<JewelType>
{
    public void Configure(EntityTypeBuilder<JewelType> builder)
    {
        builder.ToTable("JewelTypes");

        builder.HasKey(jt => jt.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(jt => jt.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(jt => jt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(jt => jt.Description)
            .HasMaxLength(500);

        builder.Property(jt => jt.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(jt => jt.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(jt => jt.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(jt => jt.Code)
            .IsUnique()
            .HasDatabaseName("IX_JewelTypes_Code");
    }
}
