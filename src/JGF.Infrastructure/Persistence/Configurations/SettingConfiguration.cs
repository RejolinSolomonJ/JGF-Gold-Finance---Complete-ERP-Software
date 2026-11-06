using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Setting"/> entity.
/// Key-value application settings (company name, GST number, financial year, etc.).
/// </summary>
public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings");

        builder.HasKey(s => s.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(s => s.DisplayName)
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        builder.Property(s => s.Category)
            .HasMaxLength(100);

        builder.Property(s => s.DataType)
            .HasMaxLength(50);

        builder.Property(s => s.IsEditable)
            .IsRequired()
            .HasDefaultValue(true);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(s => s.Key)
            .IsUnique()
            .HasDatabaseName("IX_Settings_Key");

        builder.HasIndex(s => s.Category)
            .HasDatabaseName("IX_Settings_Category");
    }
}
