using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Daily close record tracking opening/closing cash, total disbursements and collections.
/// </summary>
public class DayClose : AuditableEntity, ISoftDeletable
{
    public DateOnly CloseDate { get; set; }

    [Required]
    public DayCloseStatus Status { get; set; } = DayCloseStatus.Open;

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCashIn { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCashOut { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ClosingCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalLoansIssued { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCollections { get; set; }

    public int LoansIssuedCount { get; set; }

    public int LoansClosedCount { get; set; }

    public int ReceiptsCount { get; set; }

    [NotMapped]
    public int TotalLoansActive { get; set; }

    [NotMapped]
    public decimal TotalOutstandingPrincipal { get; set; }

    [MaxLength(1000)]
    public string? Remarks { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }

    public DateTime? ReopenedAtUtc { get; set; }
    public Guid? ReopenedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public User? ClosedByUser { get; set; }
}

