using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Notice"/> entity.
/// Represents reminder/auction notices sent to borrowers.
/// </summary>
public class NoticeConfiguration : IEntityTypeConfiguration<Notice>
{
    public void Configure(EntityTypeBuilder<Notice> builder)
    {
        builder.ToTable("Notices");

        builder.HasKey(n => n.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(n => n.NoticeType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(n => n.NoticeDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(n => n.DueDate)
            .HasColumnType("date");

        builder.Property(n => n.Content)
            .HasMaxLength(4000);

        builder.Property(n => n.IsSent)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.SentAtUtc);

        builder.Property(n => n.DeliveryMode)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.Remarks)
            .HasMaxLength(1000);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(n => n.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(n => n.LoanId)
            .HasDatabaseName("IX_Notices_LoanId");

        builder.HasIndex(n => n.NoticeDate)
            .HasDatabaseName("IX_Notices_NoticeDate");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(n => n.Loan)
            .WithMany(l => l.Notices)
            .HasForeignKey(n => n.LoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
