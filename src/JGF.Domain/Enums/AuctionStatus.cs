namespace JGF.Domain.Enums;

/// <summary>
/// Status of an auction conducted for a defaulted loan.
/// </summary>
public enum AuctionStatus
{
    /// <summary>Auction has been scheduled but not yet conducted.</summary>
    Scheduled,

    /// <summary>Auction has been conducted and jewels sold.</summary>
    Conducted,

    /// <summary>Scheduled auction was cancelled (e.g., customer paid before auction).</summary>
    Cancelled,

    /// <summary>Surplus from auction has been refunded to the customer.</summary>
    SurplusRefunded
}
