using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="LoanLedgerEntry"/> entity.
/// Loan-specific sub-ledger entries tracking interest accrual, payments, penal interest, etc.
/// </summary>
public class LoanLedgerEntryConfiguration : IEntityTypeConfiguration<LoanLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LoanLedgerEntry> builder)
    {
        builder.ToTable("LoanLedgerEntries");

        builder.HasKey(e => e.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(e => e.EntryDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(e => e.EntryType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(e => e.DebitAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(e => e.CreditAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(e => e.Balance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(e => e.Narration)
            .HasMaxLength(500);

        builder.Property(e => e.ReferenceType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(e => e.ReferenceId);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(e => new { e.LoanId, e.EntryDate })
            .HasDatabaseName("IX_LoanLedgerEntries_LoanId_EntryDate");

        builder.HasIndex(e => e.EntryType)
            .HasDatabaseName("IX_LoanLedgerEntries_EntryType");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(e => e.Loan)
            .WithMany(l => l.LoanLedgerEntries)
            .HasForeignKey(e => e.LoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
