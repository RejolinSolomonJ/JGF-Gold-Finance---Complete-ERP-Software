using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Records payments made to Banks/NBFCs for jewel repledge servicing (interest, principal, closure).
/// </summary>
public class BankPayment : AuditableEntity, ISoftDeletable
{
    public Guid BankPledgeId { get; set; }

    [Required]
    [MaxLength(30)]
    public string RzReceiptNumber { get; set; } = string.Empty;

    public DateOnly PaymentDate { get; set; }

    [Required]
    public BankPaymentType PaymentType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PrincipalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal InterestAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PostalCharges { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public Guid? VoucherId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public BankPledge BankPledge { get; set; } = null!;
    public Voucher? Voucher { get; set; }
}
