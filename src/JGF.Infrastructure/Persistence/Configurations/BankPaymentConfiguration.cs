using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class BankPaymentConfiguration : IEntityTypeConfiguration<BankPayment>
{
    public void Configure(EntityTypeBuilder<BankPayment> builder)
    {
        builder.ToTable("BankPayments");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.RzReceiptNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(b => b.PaymentDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(b => b.PaymentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(b => b.PaymentMode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.PrincipalAmount)
            .HasPrecision(18, 2);

        builder.Property(b => b.InterestAmount)
            .HasPrecision(18, 2);

        builder.Property(b => b.PostalCharges)
            .HasPrecision(18, 2);

        builder.Property(b => b.TotalAmount)
            .HasPrecision(18, 2);

        builder.HasIndex(b => b.RzReceiptNumber)
            .IsUnique()
            .HasDatabaseName("IX_BankPayments_RzReceiptNumber");

        builder.HasIndex(b => b.BankPledgeId)
            .HasDatabaseName("IX_BankPayments_BankPledgeId");

        builder.HasIndex(b => b.PaymentDate)
            .HasDatabaseName("IX_BankPayments_PaymentDate");

        builder.HasOne(b => b.BankPledge)
            .WithMany()
            .HasForeignKey(b => b.BankPledgeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Voucher)
            .WithMany()
            .HasForeignKey(b => b.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
