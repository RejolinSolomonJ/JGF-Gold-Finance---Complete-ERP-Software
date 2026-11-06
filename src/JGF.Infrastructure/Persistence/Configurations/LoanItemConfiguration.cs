using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class LoanItemConfiguration : IEntityTypeConfiguration<LoanItem>
{
    public void Configure(EntityTypeBuilder<LoanItem> builder)
    {
        builder.ToTable("LoanItems");
        builder.HasKey(li => li.Id);

        builder.Property(li => li.Description).IsRequired().HasMaxLength(300);
        builder.Property(li => li.DescriptionTamil).HasMaxLength(300);
        builder.Property(li => li.Quantity).IsRequired().HasDefaultValue(1);

        builder.Property(li => li.GrossWeight).HasPrecision(10, 3);
        builder.Property(li => li.NetWeight).HasPrecision(10, 3);
        builder.Property(li => li.DeductionWeight).HasPrecision(10, 3);
        builder.Property(li => li.PurityPercent).HasPrecision(5, 2);
        builder.Property(li => li.AppraisedValue).HasPrecision(18, 2);
        builder.Property(li => li.Notes).HasMaxLength(500);

        builder.Property(li => li.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(li => li.DeletedAtUtc);

        builder.HasIndex(li => li.LoanId).HasDatabaseName("IX_LoanItems_LoanId");

        builder.HasOne(li => li.Loan)
            .WithMany(l => l.Items)
            .HasForeignKey(li => li.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(li => li.JewelType)
            .WithMany(jt => jt.LoanItems)
            .HasForeignKey(li => li.JewelTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
