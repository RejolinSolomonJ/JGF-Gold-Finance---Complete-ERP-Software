using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class SchemeConfiguration : IEntityTypeConfiguration<Scheme>
{
    public void Configure(EntityTypeBuilder<Scheme> builder)
    {
        builder.ToTable("Schemes");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.InterestMethod).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(s => s.InterestRatePerMonth).HasPrecision(18, 4);
        builder.Property(s => s.PenalInterestRatePerMonth).HasPrecision(18, 4);
        builder.Property(s => s.MaxLtvPercent).HasPrecision(5, 2);

        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(s => s.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(s => s.DeletedAtUtc);

        builder.HasIndex(s => s.Code).IsUnique().HasDatabaseName("IX_Schemes_Code");

        builder.HasMany(s => s.Slabs)
            .WithOne(ss => ss.Scheme)
            .HasForeignKey(ss => ss.SchemeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Loans)
            .WithOne(l => l.Scheme)
            .HasForeignKey(l => l.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
