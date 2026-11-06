using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Loan"/> entity.
/// </summary>
public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans", t =>
        {
            // Check constraint: loan amount must be positive
            t.HasCheckConstraint("CK_Loans_LoanAmount_Positive", "\"LoanAmount\" > 0");
        });

        builder.HasKey(l => l.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(l => l.LoanNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(l => l.PledgeDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(l => l.MaturityDate)
            .HasColumnType("date");

        builder.Property(l => l.ClosedDate)
            .HasColumnType("date");

        builder.Property(l => l.AuctionDate)
            .HasColumnType("date");

        builder.Property(l => l.LoanAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(l => l.MarketValue)
            .HasPrecision(18, 2);

        builder.Property(l => l.InterestRate)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(l => l.PenalRate)
            .HasPrecision(18, 4);

        builder.Property(l => l.TotalWeightGross)
            .HasPrecision(10, 3);

        builder.Property(l => l.TotalWeightNet)
            .HasPrecision(10, 3);

        builder.Property(l => l.TotalStoneWeight)
            .HasPrecision(10, 3);

        builder.Property(l => l.AppraisedValue)
            .HasPrecision(18, 2);

        builder.Property(l => l.PrincipalOutstanding)
            .HasPrecision(18, 2);

        builder.Property(l => l.InterestOutstanding)
            .HasPrecision(18, 2);

        builder.Property(l => l.PenalInterestOutstanding)
            .HasPrecision(18, 2);

        builder.Property(l => l.TotalReceived)
            .HasPrecision(18, 2);

        builder.Property(l => l.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(l => l.Remarks)
            .HasMaxLength(1000);

        builder.Property(l => l.PacketNumber)
            .HasMaxLength(50);

        builder.Property(l => l.RenewalOfLoanId);

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(l => l.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(l => l.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(l => l.LoanNumber)
            .IsUnique()
            .HasDatabaseName("IX_Loans_LoanNumber");

        builder.HasIndex(l => new { l.CustomerId, l.Status })
            .HasDatabaseName("IX_Loans_CustomerId_Status");

        builder.HasIndex(l => l.PledgeDate)
            .HasDatabaseName("IX_Loans_PledgeDate");

        builder.HasIndex(l => l.SchemeId)
            .HasDatabaseName("IX_Loans_SchemeId");

        builder.HasIndex(l => l.Status)
            .HasDatabaseName("IX_Loans_Status");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(l => l.Customer)
            .WithMany(c => c.Loans)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Scheme)
            .WithMany(s => s.Loans)
            .HasForeignKey(l => l.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ApprovedByUser)
            .WithMany()
            .HasForeignKey(l => l.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.RenewalOfLoan)
            .WithMany()
            .HasForeignKey(l => l.RenewalOfLoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.LoanItems)
            .WithOne(li => li.Loan)
            .HasForeignKey(li => li.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Receipts)
            .WithOne(r => r.Loan)
            .HasForeignKey(r => r.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Notices)
            .WithOne(n => n.Loan)
            .HasForeignKey(n => n.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.LoanLedgerEntries)
            .WithOne(e => e.Loan)
            .HasForeignKey(e => e.LoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
