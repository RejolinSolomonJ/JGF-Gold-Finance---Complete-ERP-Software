using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("Receipts");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReceiptNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.ReceiptDate).HasColumnType("date");
        builder.Property(r => r.ReceiptType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.PaymentMode).IsRequired().HasConversion<string>().HasMaxLength(30);

        builder.Property(r => r.TotalAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(r => r.PrincipalAmount).HasPrecision(18, 2);
        builder.Property(r => r.InterestAmount).HasPrecision(18, 2);
        builder.Property(r => r.PenalInterestAmount).HasPrecision(18, 2);
        builder.Property(r => r.OtherCharges).HasPrecision(18, 2);
        builder.Property(r => r.PostalCharges).HasPrecision(18, 2);
        builder.Property(r => r.OtherChargesDescription).HasMaxLength(200);
        builder.Property(r => r.ReferenceNumber).HasMaxLength(100);
        builder.Property(r => r.Notes).HasMaxLength(500);

        builder.Property(r => r.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.DeletedAtUtc);

        builder.HasIndex(r => r.ReceiptNumber).IsUnique().HasDatabaseName("IX_Receipts_ReceiptNumber");
        builder.HasIndex(r => r.LoanId).HasDatabaseName("IX_Receipts_LoanId");
        builder.HasIndex(r => r.ReceiptDate).HasDatabaseName("IX_Receipts_ReceiptDate");

        builder.HasOne(r => r.Loan)
            .WithMany(l => l.Receipts)
            .HasForeignKey(r => r.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Voucher)
            .WithMany()
            .HasForeignKey(r => r.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
