using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JGF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="VoucherLine"/> entity.
/// Individual debit or credit line within a double-entry voucher.
/// </summary>
public class VoucherLineConfiguration : IEntityTypeConfiguration<VoucherLine>
{
    public void Configure(EntityTypeBuilder<VoucherLine> builder)
    {
        builder.ToTable("VoucherLines", t =>
        {
            // Check: exactly one of Debit or Credit must be > 0, not both
            t.HasCheckConstraint("CK_VoucherLines_DebitOrCredit",
                "(\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");
        });

        builder.HasKey(vl => vl.Id);

        // ── Scalar Properties ─────────────────────────────────────
        builder.Property(vl => vl.DebitAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(vl => vl.CreditAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(vl => vl.Narration)
            .HasMaxLength(500);

        builder.Property(vl => vl.LineOrder)
            .IsRequired();

        // ── Soft Delete ───────────────────────────────────────────
        builder.Property(vl => vl.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(vl => vl.DeletedAtUtc);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(vl => vl.VoucherId)
            .HasDatabaseName("IX_VoucherLines_VoucherId");

        builder.HasIndex(vl => vl.LedgerHeadId)
            .HasDatabaseName("IX_VoucherLines_LedgerHeadId");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(vl => vl.Voucher)
            .WithMany(v => v.Lines)
            .HasForeignKey(vl => vl.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(vl => vl.LedgerHead)
            .WithMany()
            .HasForeignKey(vl => vl.LedgerHeadId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
