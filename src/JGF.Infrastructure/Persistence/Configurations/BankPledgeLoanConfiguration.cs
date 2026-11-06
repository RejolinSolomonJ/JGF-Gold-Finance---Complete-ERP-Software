using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="BankPledgeLoan"/> entity.
/// Many-to-many join entity linking BankPledge to Loan (which loans are pledged to which bank pledge).
/// </summary>
public class BankPledgeLoanConfiguration : IEntityTypeConfiguration<BankPledgeLoan>
{
    public void Configure(EntityTypeBuilder<BankPledgeLoan> builder)
    {
        builder.ToTable("BankPledgeLoans");

        builder.HasKey(bpl => bpl.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(bpl => bpl.PledgedAmount)
            .HasPrecision(18, 2);

        builder.Property(bpl => bpl.PledgedDate)
            .HasColumnType("date");

        builder.Property(bpl => bpl.ReleasedDate)
            .HasColumnType("date");

        builder.Property(bpl => bpl.IsReleased)
            .IsRequired()
            .HasDefaultValue(false);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(bpl => bpl.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(bpl => bpl.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        // Composite unique: a loan can only be in a bank pledge once
        builder.HasIndex(bpl => new { bpl.BankPledgeId, bpl.LoanId })
            .IsUnique()
            .HasDatabaseName("IX_BankPledgeLoans_BankPledgeId_LoanId");

        builder.HasIndex(bpl => bpl.LoanId)
            .HasDatabaseName("IX_BankPledgeLoans_LoanId");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(bpl => bpl.BankPledge)
            .WithMany(bp => bp.BankPledgeLoans)
            .HasForeignKey(bpl => bpl.BankPledgeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(bpl => bpl.Loan)
            .WithMany(l => l.BankPledgeLoans)
            .HasForeignKey(bpl => bpl.LoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
