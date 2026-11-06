using System.ComponentModel.DataAnnotations;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Bank or NBFC master for fund sources and jewel repledge.
/// </summary>
public class Bank : AuditableEntity, ISoftDeletable
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Branch { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [MaxLength(15)]
    public string? IfscCode { get; set; }

    [MaxLength(200)]
    public string? ContactPerson { get; set; }

    [MaxLength(15)]
    public string? ContactPhone { get; set; }

    public decimal InterestRate { get; set; }

    public decimal SanctionedLimit { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Remarks { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<BankPledge> BankPledges { get; set; } = new List<BankPledge>();
}

