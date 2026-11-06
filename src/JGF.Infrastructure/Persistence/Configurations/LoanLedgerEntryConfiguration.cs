using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

public class LoanLedgerEntryConfiguration : IEntityTypeConfiguration<LoanLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LoanLedgerEntry> builder)
    {
        builder.ToTable("LoanLedgerEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntryDate).IsRequired().HasColumnType("date");
        builder.Property(e => e.EntryType).IsRequired().HasConversion<string>().HasMaxLength(30);

        builder.Property(e => e.DebitAmount).IsRequired().HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(e => e.CreditAmount).IsRequired().HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(e => e.Balance).IsRequired().HasPrecision(18, 2);

        builder.Property(e => e.Narration).HasMaxLength(500);
        builder.Property(e => e.ReferenceType).HasMaxLength(50);
        builder.Property(e => e.ReferenceId);

        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedAtUtc);

        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LoanLedgerEntries_LoanId");
        builder.HasIndex(e => e.EntryDate).HasDatabaseName("IX_LoanLedgerEntries_EntryDate");

        builder.HasOne(e => e.Loan)
            .WithMany(l => l.LedgerEntries)
            .HasForeignKey(e => e.LoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
