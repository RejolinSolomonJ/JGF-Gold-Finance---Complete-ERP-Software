using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Customer"/> entity.
/// </summary>
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        // ── Primary Key ───────────────────────────────────────────
        builder.HasKey(c => c.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.GuardianName)
            .HasMaxLength(200);

        builder.Property(c => c.Gender)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(c => c.DateOfBirth)
            .HasColumnType("date");

        builder.Property(c => c.AadhaarNumber)
            .HasMaxLength(12);

        builder.Property(c => c.PanNumber)
            .HasMaxLength(10);

        builder.Property(c => c.MobileNumber)
            .IsRequired()
            .HasMaxLength(15);

        builder.Property(c => c.AlternateMobile)
            .HasMaxLength(15);

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        builder.Property(c => c.Address)
            .HasMaxLength(500);

        builder.Property(c => c.City)
            .HasMaxLength(100);

        builder.Property(c => c.District)
            .HasMaxLength(100);

        builder.Property(c => c.State)
            .HasMaxLength(100);

        builder.Property(c => c.Pincode)
            .HasMaxLength(10);

        builder.Property(c => c.PhotoPath)
            .HasMaxLength(500);

        builder.Property(c => c.IdProofPath)
            .HasMaxLength(500);

        builder.Property(c => c.AddressProofPath)
            .HasMaxLength(500);

        builder.Property(c => c.Occupation)
            .HasMaxLength(200);

        builder.Property(c => c.Remarks)
            .HasMaxLength(1000);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("IX_Customers_Code");

        builder.HasIndex(c => c.MobileNumber)
            .HasDatabaseName("IX_Customers_MobileNumber");

        builder.HasIndex(c => c.AadhaarNumber)
            .HasDatabaseName("IX_Customers_AadhaarNumber")
            .HasFilter("\"AadhaarNumber\" IS NOT NULL");

        // ── Navigation ────────────────────────────────────────────
        builder.HasMany(c => c.Loans)
            .WithOne(l => l.Customer)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
