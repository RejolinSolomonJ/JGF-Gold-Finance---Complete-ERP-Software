using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for BackupLog.
/// </summary>
public class BackupLogConfiguration : IEntityTypeConfiguration<BackupLog>
{
    public void Configure(EntityTypeBuilder<BackupLog> builder)
    {
        builder.ToTable("BackupLogs");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.BackupType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(b => b.CloudUploadStatus)
            .HasMaxLength(500);

        builder.Property(b => b.LanCopyStatus)
            .HasMaxLength(500);

        builder.HasIndex(b => b.BackupDate)
            .HasDatabaseName("IX_BackupLogs_BackupDate");

        builder.HasIndex(b => b.Status)
            .HasDatabaseName("IX_BackupLogs_Status");
    }
}
