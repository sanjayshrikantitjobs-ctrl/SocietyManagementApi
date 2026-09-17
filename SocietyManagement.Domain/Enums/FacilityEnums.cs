namespace SocietyManagement.Domain.Enums;

public enum FacilityType
{
    ClubHouse = 1,
    CommunityHall = 2,
    FunctionRoom = 3,
    RefugeRoom = 4,
    PartyHall = 5,
    Terrace = 6,
    Other = 7
}

public enum FacilityPricingType
{
    PerHour = 1,
    PerDay = 2,
    Lumpsum = 3
}

/// <summary>Pending is the initial state for a facility that RequiresApproval;
/// a facility booked direct (RequiresApproval = false) starts at Approved.
/// PaymentPending/PaymentCompleted track the separate payment-collection
/// side of an already-Approved booking — mirrors FacilityBooking's own
/// PaymentStatus for the "amount owed" lifecycle, kept as booking-level
/// states too so the resident's My Bookings list can show one status.</summary>
public enum FacilityBookingStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
    PaymentPending = 6,
    PaymentCompleted = 7
}

public enum FacilityPaymentStatus
{
    Pending = 1,
    Completed = 2,
    Refunded = 3
}
