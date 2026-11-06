using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Daily gold rate record. Stores rates for different purities used
/// for appraising pledged jewellery on a given date.
/// </summary>
public class GoldRate : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Date for which this gold rate applies. One record per date.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Standard market rate per gram (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RatePerGram { get; set; }

    /// <summary>
    /// Rate per gram for 22-karat gold (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Rate22K { get; set; }

    /// <summary>
    /// Rate per gram for 24-karat gold (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Rate24K { get; set; }

    /// <summary>
    /// Rate per gram for 18-karat gold (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Rate18K { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Guid? EnteredByUserId { get; set; }

    // Backward compatibility aliases
    [NotMapped]
    public decimal RatePerGram22K
    {
        get => Rate22K;
        set => Rate22K = value;
    }

    [NotMapped]
    public decimal RatePerGram24K
    {
        get => Rate24K;
        set => Rate24K = value;
    }

    [NotMapped]
    public decimal RatePerGram18K
    {
        get => Rate18K;
        set => Rate18K = value;
    }

    [NotMapped]
    public decimal MarketRatePerGram
    {
        get => RatePerGram;
        set => RatePerGram = value;
    }
}
