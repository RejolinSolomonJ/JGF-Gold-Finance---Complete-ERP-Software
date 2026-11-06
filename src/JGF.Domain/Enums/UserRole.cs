namespace JGF.Domain.Enums;

/// <summary>
/// User role in the JGF application (authorization level).
/// </summary>
public enum UserRole
{
    /// <summary>Business owner — full access to all features.</summary>
    Owner,

    /// <summary>Branch manager — access to most features except system settings.</summary>
    Manager,

    /// <summary>Cashier — access to day-to-day loan and receipt operations.</summary>
    Cashier
}
