using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class SchemeSlabConfiguration : IEntityTypeConfiguration<SchemeSlab>
{
    public void Configure(EntityTypeBuilder<SchemeSlab> builder)
    {
        builder.ToTable("SchemeSlabs");
        builder.HasKey(ss => ss.Id);

        builder.Property(ss => ss.MinAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(ss => ss.MaxAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(ss => ss.InterestRatePerMonth).IsRequired().HasPrecision(18, 4);

        builder.Property(ss => ss.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(ss => ss.DeletedAtUtc);

        builder.HasIndex(ss => ss.SchemeId).HasDatabaseName("IX_SchemeSlabs_SchemeId");

        builder.HasOne(ss => ss.Scheme)
            .WithMany(s => s.Slabs)
            .HasForeignKey(ss => ss.SchemeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
