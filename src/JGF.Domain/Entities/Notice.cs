using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Notice sent to a borrower regarding upcoming maturity, overdue status, or impending auction.
/// </summary>
public class Notice : AuditableEntity, ISoftDeletable
{
    public Guid LoanId { get; set; }

    [Required]
    public NoticeType NoticeType { get; set; }

    public DateOnly NoticeDate { get; set; }

    public DateOnly? DueDate { get; set; }

    [MaxLength(4000)]
    public string? Content { get; set; }

    public bool IsSent { get; set; } = false;

    public DateTime? SentAtUtc { get; set; }

    public NoticeSentVia? DeliveryMode { get; set; }

    [NotMapped]
    public NoticeSentVia? SentVia
    {
        get => DeliveryMode;
        set => DeliveryMode = value;
    }

    [NotMapped]
    public string? TrackingNumber
    {
        get => Remarks;
        set => Remarks = value;
    }

    [MaxLength(1000)]
    public string? Remarks { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Loan Loan { get; set; } = null!;
}

