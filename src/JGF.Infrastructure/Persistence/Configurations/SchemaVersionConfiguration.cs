using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for SchemaVersion.
/// </summary>
public class SchemaVersionConfiguration : IEntityTypeConfiguration<SchemaVersion>
{
    public void Configure(EntityTypeBuilder<SchemaVersion> builder)
    {
        builder.ToTable("SchemaVersions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AppVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.AppliedMigrations)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(s => s.AppliedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.PreUpdateBackupPath)
            .HasMaxLength(500);

        builder.Property(s => s.IntegrityCheckStatus)
            .HasMaxLength(500);

        builder.Property(s => s.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(s => s.AppVersion)
            .HasDatabaseName("IX_SchemaVersions_AppVersion");

        builder.HasIndex(s => s.AppliedAtUtc)
            .HasDatabaseName("IX_SchemaVersions_AppliedAtUtc");
    }
}

