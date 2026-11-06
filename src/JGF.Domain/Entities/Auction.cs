using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Auction process record when unredeemed ornaments are liquidated.
/// </summary>
public class Auction : AuditableEntity, ISoftDeletable
{
    public Guid LoanId { get; set; }

    public DateOnly AuctionDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalePrice { get; set; }

    [MaxLength(200)]
    public string? BuyerName { get; set; }

    [MaxLength(500)]
    public string? BuyerAddress { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingPrincipal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingInterest { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AuctionCharges { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SurplusAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DeficitAmount { get; set; }

    [Required]
    public AuctionStatus Status { get; set; } = AuctionStatus.Scheduled;

    public Guid? VoucherId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Loan Loan { get; set; } = null!;
}

