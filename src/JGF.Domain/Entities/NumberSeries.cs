using System.ComponentModel.DataAnnotations;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Number series generator for gapless, zero-duplicate auto-numbering.
/// </summary>
public class NumberSeries : AuditableEntity, ISoftDeletable
{
    [Required]
    public NumberSeriesType SeriesType { get; set; }

    [Required]
    [MaxLength(10)]
    public string Prefix { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string FinancialYear { get; set; } = string.Empty;

    public int LastNumber { get; set; } = 0;

    public int PadLength { get; set; } = 4;

    [MaxLength(5)]
    public string Separator { get; set; } = "/";

    public bool IsActive { get; set; } = true;

    public Guid? SchemeId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Scheme? Scheme { get; set; }
}

