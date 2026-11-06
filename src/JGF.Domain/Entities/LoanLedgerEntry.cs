using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Sub-ledger transaction entry tracking the running financial balance of a loan.
/// </summary>
public class LoanLedgerEntry : AuditableEntity, ISoftDeletable
{
    public Guid LoanId { get; set; }

    public DateOnly EntryDate { get; set; }

    [Required]
    public LoanLedgerEntryType EntryType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DebitAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    [MaxLength(500)]
    public string? Narration { get; set; }

    [MaxLength(50)]
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Loan Loan { get; set; } = null!;
}

