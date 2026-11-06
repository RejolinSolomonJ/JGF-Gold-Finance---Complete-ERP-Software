using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(20);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameTamil).HasMaxLength(200);
        builder.Property(c => c.FatherOrHusbandName).HasMaxLength(200);
        builder.Property(c => c.Gender).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Phone).IsRequired().HasMaxLength(15);
        builder.Property(c => c.Phone2).HasMaxLength(15);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.AddressTamil).HasMaxLength(500);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.Pincode).HasMaxLength(10);
        builder.Property(c => c.AadhaarNumber).HasMaxLength(20);
        builder.Property(c => c.PanNumber).HasMaxLength(20);
        builder.Property(c => c.PhotoPath).HasMaxLength(500);
        builder.Property(c => c.Notes).HasMaxLength(1000);

        builder.Property(c => c.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(c => c.DeletedAtUtc);

        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("IX_Customers_Code");
        builder.HasIndex(c => c.Phone).HasDatabaseName("IX_Customers_Phone");

        builder.HasMany(c => c.Loans)
            .WithOne(l => l.Customer)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
