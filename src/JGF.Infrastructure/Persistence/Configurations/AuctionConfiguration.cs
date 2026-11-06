using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Auction entity.
/// </summary>
public class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.ToTable("Auctions");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AuctionDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(a => a.ReservePrice)
            .HasPrecision(18, 2);

        builder.Property(a => a.SalePrice)
            .HasPrecision(18, 2);

        builder.Property(a => a.OutstandingPrincipal)
            .HasPrecision(18, 2);

        builder.Property(a => a.OutstandingInterest)
            .HasPrecision(18, 2);

        builder.Property(a => a.AuctionCharges)
            .HasPrecision(18, 2);

        builder.Property(a => a.SurplusAmount)
            .HasPrecision(18, 2);

        builder.Property(a => a.DeficitAmount)
            .HasPrecision(18, 2);

        builder.Property(a => a.BuyerName)
            .HasMaxLength(200);

        builder.Property(a => a.BuyerAddress)
            .HasMaxLength(500);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.Notes)
            .HasMaxLength(1000);

        builder.Property(a => a.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(a => a.LoanId)
            .HasDatabaseName("IX_Auctions_LoanId");

        builder.HasIndex(a => a.AuctionDate)
            .HasDatabaseName("IX_Auctions_AuctionDate");

        builder.HasOne(a => a.Loan)
            .WithMany(l => l.Auctions)
            .HasForeignKey(a => a.LoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

