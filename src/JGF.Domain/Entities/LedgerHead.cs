using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Chart of accounts — each ledger head represents a distinct financial account.
/// </summary>
public class LedgerHead : AuditableEntity, ISoftDeletable
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public LedgerHeadType AccountGroup { get; set; }

    [Required]
    [MaxLength(10)]
    public string NormalBalance { get; set; } = "Debit";

    public bool IsSystemAccount { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public Guid? ParentLedgerHeadId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; } = 0m;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public LedgerHead? ParentLedgerHead { get; set; }
    public ICollection<LedgerHead> ChildLedgerHeads { get; set; } = new List<LedgerHead>();
}
