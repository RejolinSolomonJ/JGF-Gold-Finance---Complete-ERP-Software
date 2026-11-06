using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents an identity or address document uploaded for a customer (KYC).
/// </summary>
public class CustomerDocument : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Foreign key to the customer who owns this document.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Type of document (Aadhaar, PAN, Voter ID, etc.).
    /// </summary>
    [Required]
    [MaxLength(30)]
    public DocumentType DocumentType { get; set; }

    /// <summary>
    /// Document number / reference (e.g., Aadhaar number, PAN number).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Relative path to the scanned copy of the document.
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    /// <summary>
    /// User who verified this document. Null if not yet verified.
    /// </summary>
    public Guid? VerifiedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp when the document was verified. Null if not yet verified.
    /// </summary>
    public DateTime? VerifiedAtUtc { get; set; }

    // ── Soft Delete ──────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAtUtc { get; set; }

    // ── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// The customer this document belongs to.
    /// </summary>
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// The user who verified this document.
    /// </summary>
    public User? VerifiedByUser { get; set; }
}
