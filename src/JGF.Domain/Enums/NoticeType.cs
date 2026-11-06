namespace JGF.Domain.Enums;

/// <summary>
/// Type of notice sent to a customer regarding an overdue loan.
/// </summary>
public enum NoticeType
{
    /// <summary>First polite reminder to the customer.</summary>
    FirstReminder,

    /// <summary>Second reminder with stronger language.</summary>
    SecondReminder,

    /// <summary>Final notice before auction proceedings.</summary>
    FinalNotice,

    /// <summary>Formal auction notice as per RBI guidelines.</summary>
    AuctionNotice,

    // Aliases
    Reminder = FirstReminder,
    DemandNotice = SecondReminder
}
