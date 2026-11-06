namespace JGF.Domain.Enums;

/// <summary>
/// Mode of delivery for notices sent to customers.
/// </summary>
public enum NoticeSentVia
{
    /// <summary>Notice delivered by hand.</summary>
    Hand,

    /// <summary>Notice sent via India Post.</summary>
    Post,

    /// <summary>Notice sent via courier service.</summary>
    Courier,

    /// <summary>Notice sent via SMS.</summary>
    SMS,

    /// <summary>Notice sent via WhatsApp message.</summary>
    WhatsApp,

    // Backward-compatible aliases
    RegisteredPost = Post,
    SpeedPost = Post,
    HandDelivery = Hand,
    Sms = SMS
}
