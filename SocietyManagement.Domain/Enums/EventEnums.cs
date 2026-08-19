namespace SocietyManagement.Domain.Enums;

public enum EventStatus
{
    Draft = 1,
    Open = 2,
    Closed = 3,
    Completed = 4,
    Cancelled = 5
}

public enum EventRsvpStatus
{
    Registered = 1,
    CheckedIn = 2,
    Cancelled = 3
}
