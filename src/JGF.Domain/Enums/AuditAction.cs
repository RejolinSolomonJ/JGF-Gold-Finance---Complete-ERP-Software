namespace JGF.Domain.Enums;

/// <summary>
/// Type of action recorded in the audit log.
/// </summary>
public enum AuditAction
{
    /// <summary>A new record was created.</summary>
    Create,

    /// <summary>An existing record was updated.</summary>
    Update,

    /// <summary>A record was soft-deleted.</summary>
    Delete,

    /// <summary>A user logged in successfully.</summary>
    Login,

    /// <summary>A user logged out.</summary>
    Logout,

    /// <summary>A login attempt failed (wrong credentials).</summary>
    FailedLogin
}
