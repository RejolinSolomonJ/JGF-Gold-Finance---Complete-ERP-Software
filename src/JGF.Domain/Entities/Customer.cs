using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Represents a customer (borrower/pledger) who avails gold loans.
/// Stores KYC information, contact details, and bilingual name/address.
/// </summary>
public class Customer : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Unique auto-generated customer code (e.g., "C0001").
    /// Used as a human-readable identifier on receipts and reports.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the customer in English.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Alias property for FullName
    /// </summary>
    [NotMapped]
    public string FullName
    {
        get => Name;
        set => Name = value;
    }

    /// <summary>
    /// Full name of the customer in Tamil (தமிழ்).
    /// </summary>
    [MaxLength(200)]
    public string? NameTamil { get; set; }

    /// <summary>
    /// Father's or husband's name (required for KYC / pawn ticket).
    /// </summary>
    [MaxLength(200)]
    public string? FatherOrHusbandName { get; set; }

    [NotMapped]
    public string? GuardianName
    {
        get => FatherOrHusbandName;
        set => FatherOrHusbandName = value;
    }

    /// <summary>
    /// Gender of the customer.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public Gender Gender { get; set; }

    /// <summary>
    /// Date of birth of the customer. Nullable if not provided.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Primary phone number.
    /// </summary>
    [Required]
    [MaxLength(15)]
    public string Phone { get; set; } = string.Empty;

    [NotMapped]
    public string MobileNumber
    {
        get => Phone;
        set => Phone = value;
    }

    /// <summary>
    /// Secondary/alternate phone number.
    /// </summary>
    [MaxLength(15)]
    public string? Phone2 { get; set; }

    [NotMapped]
    public string? AlternateMobile
    {
        get => Phone2;
        set => Phone2 = value;
    }

    /// <summary>
    /// Full address in English.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Full address in Tamil (தமிழ்).
    /// </summary>
    [MaxLength(500)]
    public string? AddressTamil { get; set; }

    /// <summary>
    /// City or town of residence.
    /// </summary>
    [MaxLength(100)]
    public string? City { get; set; }

    [NotMapped]
    public string? District
    {
        get => City;
        set => City = value;
    }

    /// <summary>
    /// Indian PIN code (6 digits).
    /// </summary>
    [MaxLength(10)]
    public string? Pincode { get; set; }

    /// <summary>
    /// Aadhaar number (12-digit UID). Stored masked/encrypted in production.
    /// </summary>
    [MaxLength(20)]
    public string? AadhaarNumber { get; set; }

    /// <summary>
    /// PAN number (10-character alphanumeric). Required for loans above ₹5,00,000.
    /// </summary>
    [MaxLength(20)]
    public string? PanNumber { get; set; }

    /// <summary>
    /// Relative path to the customer's photograph file.
    /// </summary>
    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    /// <summary>
    /// Free-text notes about the customer.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Concurrency token for optimistic locking across multiple workstations.
    /// </summary>
    [ConcurrencyCheck]
    public int ConcurrencyVersion { get; set; } = 1;

    // ── Soft Delete ──────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAtUtc { get; set; }

    // ── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// Collection of identity/address documents submitted by this customer.
    /// </summary>
    public ICollection<CustomerDocument> Documents { get; set; } = new List<CustomerDocument>();

    /// <summary>
    /// Collection of loans taken by this customer.
    /// </summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
