using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Receipt"/> entity.
/// Represents payments received from borrowers (interest, principal, closure, etc.).
/// </summary>
public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("Receipts");

        builder.HasKey(r => r.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(r => r.ReceiptNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.ReceiptDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(r => r.ReceiptType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.PaymentMode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.TotalAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(r => r.PrincipalAmount)
            .HasPrecision(18, 2);

        builder.Property(r => r.InterestAmount)
            .HasPrecision(18, 2);

        builder.Property(r => r.PenalInterestAmount)
            .HasPrecision(18, 2);

        builder.Property(r => r.OtherCharges)
            .HasPrecision(18, 2);

        builder.Property(r => r.Discount)
            .HasPrecision(18, 2);

        builder.Property(r => r.ExcessRefund)
            .HasPrecision(18, 2);

        builder.Property(r => r.ReferenceNumber)
            .HasMaxLength(100);

        builder.Property(r => r.BankName)
            .HasMaxLength(200);

        builder.Property(r => r.Remarks)
            .HasMaxLength(1000);

        builder.Property(r => r.IsCancelled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.CancelledAtUtc);

        builder.Property(r => r.CancellationReason)
            .HasMaxLength(500);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(r => r.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(r => r.ReceiptNumber)
            .IsUnique()
            .HasDatabaseName("IX_Receipts_ReceiptNumber");

        builder.HasIndex(r => r.LoanId)
            .HasDatabaseName("IX_Receipts_LoanId");

        builder.HasIndex(r => r.ReceiptDate)
            .HasDatabaseName("IX_Receipts_ReceiptDate");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(r => r.Loan)
            .WithMany(l => l.Receipts)
            .HasForeignKey(r => r.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CollectedByUser)
            .WithMany()
            .HasForeignKey(r => r.CollectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Voucher)
            .WithMany()
            .HasForeignKey(r => r.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
