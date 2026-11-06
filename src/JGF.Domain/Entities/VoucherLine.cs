using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Double-entry line item within a voucher.
/// </summary>
public class VoucherLine : AuditableEntity, ISoftDeletable
{
    public Guid VoucherId { get; set; }
    public Guid LedgerHeadId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DebitAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAmount { get; set; }

    [MaxLength(500)]
    public string? Narration { get; set; }

    public int LineOrder { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Voucher Voucher { get; set; } = null!;
    public LedgerHead LedgerHead { get; set; } = null!;
}

