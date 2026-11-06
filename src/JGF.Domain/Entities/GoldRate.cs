using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;

namespace JGF.Domain.Entities;

/// <summary>
/// Daily gold rate record. Stores rates for different purities used
/// for appraising pledged jewellery on a given date.
/// </summary>
public class GoldRate : AuditableEntity
{
    /// <summary>
    /// Date for which this gold rate applies. One record per date.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Rate per gram for 22-karat gold (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RatePerGram22K { get; set; }

    /// <summary>
    /// Rate per gram for 24-karat gold (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RatePerGram24K { get; set; }

    /// <summary>
    /// Rate per gram for 18-karat gold (in ₹).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RatePerGram18K { get; set; }

    /// <summary>
    /// Current market rate per gram (in ₹) — typically the retail/MCX rate for reference.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal MarketRatePerGram { get; set; }

    /// <summary>
    /// Foreign key to the user who entered/updated this rate.
    /// </summary>
    public Guid? EnteredByUserId { get; set; }

    // ── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// The user who entered this gold rate.
    /// </summary>
    public User? EnteredByUser { get; set; }
}
