namespace JGF.Domain.Enums;

/// <summary>
/// Type of identity/address document submitted by a customer.
/// </summary>
public enum DocumentType
{
    /// <summary>Aadhaar card (12-digit UID).</summary>
    Aadhaar,

    /// <summary>Permanent Account Number card (Income Tax).</summary>
    PAN,

    /// <summary>Voter identity card (EPIC).</summary>
    VoterId,

    /// <summary>Driving licence issued by RTO.</summary>
    DrivingLicense,

    /// <summary>Indian passport.</summary>
    Passport,

    /// <summary>Public distribution system ration card.</summary>
    RationCard,

    /// <summary>Any other identity document.</summary>
    Other
}
