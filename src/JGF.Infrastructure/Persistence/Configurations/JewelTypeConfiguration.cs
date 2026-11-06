using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class JewelTypeConfiguration : IEntityTypeConfiguration<JewelType>
{
    public void Configure(EntityTypeBuilder<JewelType> builder)
    {
        builder.ToTable("JewelTypes");
        builder.HasKey(jt => jt.Id);

        builder.Property(jt => jt.Code).IsRequired().HasMaxLength(20);
        builder.Property(jt => jt.Name).IsRequired().HasMaxLength(100);
        builder.Property(jt => jt.NameTamil).HasMaxLength(100);
        builder.Property(jt => jt.DefaultPurityPercent).HasPrecision(5, 2);

        builder.Property(jt => jt.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(jt => jt.DeletedAtUtc);

        builder.HasIndex(jt => jt.Code).IsUnique().HasDatabaseName("IX_JewelTypes_Code");
    }
}
