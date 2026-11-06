using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Voucher"/> entity.
/// Accounting vouchers (Journal, Receipt, Payment, Contra) with double-entry validation.
/// </summary>
public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers", t =>
        {
            // Check constraint: When posted, TotalDebit must equal TotalCredit
            t.HasCheckConstraint("CK_Vouchers_DebitEqualsCredit",
                "\"IsPosted\" = false OR \"TotalDebit\" = \"TotalCredit\"");
        });

        builder.HasKey(v => v.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(v => v.VoucherNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(v => v.VoucherDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(v => v.VoucherType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(v => v.TotalDebit)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(v => v.TotalCredit)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(v => v.Narration)
            .HasMaxLength(1000);

        builder.Property(v => v.IsPosted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.PostedAtUtc);

        builder.Property(v => v.IsCancelled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.CancelledAtUtc);

        builder.Property(v => v.CancellationReason)
            .HasMaxLength(500);

        builder.Property(v => v.ReferenceType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(v => v.ReferenceId);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(v => v.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(v => v.VoucherNumber)
            .IsUnique()
            .HasDatabaseName("IX_Vouchers_VoucherNumber");

        builder.HasIndex(v => v.VoucherDate)
            .HasDatabaseName("IX_Vouchers_VoucherDate");

        builder.HasIndex(v => v.VoucherType)
            .HasDatabaseName("IX_Vouchers_VoucherType");

        // ── Navigation ────────────────────────────────────────────
        builder.HasMany(v => v.Lines)
            .WithOne(vl => vl.Voucher)
            .HasForeignKey(vl => vl.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.PostedByUser)
            .WithMany()
            .HasForeignKey(v => v.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
