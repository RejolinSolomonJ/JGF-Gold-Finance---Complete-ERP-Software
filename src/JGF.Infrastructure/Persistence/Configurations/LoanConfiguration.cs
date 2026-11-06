using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LoanNumber).IsRequired().HasMaxLength(50);
        builder.Property(l => l.PledgeDate).HasColumnType("date");
        builder.Property(l => l.MaturityDate).HasColumnType("date");
        builder.Property(l => l.ClosedDate).HasColumnType("date");

        builder.Property(l => l.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(l => l.GrossWeight).HasPrecision(10, 3);
        builder.Property(l => l.NetWeight).HasPrecision(10, 3);
        builder.Property(l => l.DeductionWeight).HasPrecision(10, 3);
        builder.Property(l => l.MarketValue).HasPrecision(18, 2);
        builder.Property(l => l.LoanAmount).HasPrecision(18, 2);

        builder.Property(l => l.NomineeName).HasMaxLength(200);
        builder.Property(l => l.NomineeRelationship).HasMaxLength(50);
        builder.Property(l => l.PacketNumber).HasMaxLength(50);
        builder.Property(l => l.JewelPhotoPath).HasMaxLength(500);
        builder.Property(l => l.OwnerLtvOverrideReason).HasMaxLength(500);

        builder.Property(l => l.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(l => l.DeletedAtUtc);

        builder.HasIndex(l => l.LoanNumber).IsUnique().HasDatabaseName("IX_Loans_LoanNumber");
        builder.HasIndex(l => l.CustomerId).HasDatabaseName("IX_Loans_CustomerId");
        builder.HasIndex(l => l.Status).HasDatabaseName("IX_Loans_Status");

        builder.HasOne(l => l.Customer)
            .WithMany(c => c.Loans)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Scheme)
            .WithMany(s => s.Loans)
            .HasForeignKey(l => l.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Items)
            .WithOne(li => li.Loan)
            .HasForeignKey(li => li.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Receipts)
            .WithOne(r => r.Loan)
            .HasForeignKey(r => r.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Notices)
            .WithOne(n => n.Loan)
            .HasForeignKey(n => n.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.LedgerEntries)
            .WithOne(e => e.Loan)
            .HasForeignKey(e => e.LoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
