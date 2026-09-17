namespace SocietyManagement.Domain.Enums;

public enum AnnouncementType
{
    General = 1,
    Meeting = 2,
    Festival = 3,
    Maintenance = 4,
    UtilityNotice = 5,
    LostAndFound = 6,
    Emergency = 7,
    Event = 8,
    Parking = 9,
    Security = 10,
    Important = 11,
    Other = 12
}

public enum AnnouncementPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

/// <summary>Draft = not visible to residents yet. Scheduled = PublishAt is in
/// the future; AnnouncementLifecycleService flips it to Published (and fires
/// the SignalR notification) once that time passes. Published = visible now.
/// Expired = past ExpiryAt, same service flips it automatically — a society
/// admin never has to manually clean these up.</summary>
public enum AnnouncementStatus
{
    Draft = 1,
    Scheduled = 2,
    Published = 3,
    Expired = 4
}
