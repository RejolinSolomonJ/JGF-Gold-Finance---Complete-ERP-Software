using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Double-entry accounting voucher header (Receipt, Payment, Journal, Contra).
/// </summary>
public class Voucher : AuditableEntity, ISoftDeletable
{
    [Required]
    [MaxLength(30)]
    public string VoucherNumber { get; set; } = string.Empty;

    public DateOnly VoucherDate { get; set; }

    [Required]
    public VoucherType VoucherType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCredit { get; set; }

    [MaxLength(1000)]
    public string? Narration { get; set; }

    public bool IsPosted { get; set; } = true;

    public DateTime? PostedAtUtc { get; set; }

    public Guid? PostedByUserId { get; set; }

    public bool IsCancelled { get; set; } = false;

    public DateTime? CancelledAtUtc { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    [MaxLength(50)]
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public User? PostedByUser { get; set; }
    public ICollection<VoucherLine> Lines { get; set; } = new List<VoucherLine>();

    [NotMapped]
    public ICollection<VoucherLine> Entries => Lines;
}

