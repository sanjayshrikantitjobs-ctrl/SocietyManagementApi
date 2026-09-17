namespace SocietyManagement.Domain.Enums;

public enum AssetCategory
{
    Chairs = 1,
    Tables = 2,
    Speakers = 3,
    Microphones = 4,
    Projectors = 5,
    Crockery = 6,
    Decoration = 7,
    FansAndLights = 8,
    Other = 9
}

public enum AssetPricingType
{
    PerItem = 1,
    PerHour = 2,
    PerDay = 3,
    Lumpsum = 4
}

/// <summary>Issued/Returned track physical handover independent of this
/// status — see AssetBookingItem's QuantityIssued/Returned/Damaged/Lost.
/// Completed means every item on the booking has been returned and any
/// deposit refund settled.</summary>
public enum AssetBookingStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
    PaymentPending = 6,
    PaymentCompleted = 7
}
