namespace JGF.Domain.Enums;

/// <summary>
/// Method used to calculate interest on a loan scheme.
/// </summary>
public enum InterestMethod
{
    /// <summary>Simple interest — interest computed on original principal only.</summary>
    Simple,

    /// <summary>Compound interest — interest accrues on accumulated interest as well.</summary>
    Compound
}
