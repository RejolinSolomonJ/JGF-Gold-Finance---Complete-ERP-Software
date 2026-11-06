namespace JGF.Domain.Enums;

/// <summary>
/// Status of the daily close process.
/// </summary>
public enum DayCloseStatus
{
    /// <summary>Day is currently open for transactions.</summary>
    Open,

    /// <summary>Day has been closed; no further transactions allowed.</summary>
    Closed,

    /// <summary>A previously closed day was reopened for corrections.</summary>
    Reopened
}
