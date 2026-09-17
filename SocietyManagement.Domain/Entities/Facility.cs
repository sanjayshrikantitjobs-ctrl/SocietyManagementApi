using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class Facility : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;

    public FacilityType Type { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public string? Location { get; set; }

    public int Capacity { get; set; }

    public FacilityPricingType PricingType { get; set; }

    public decimal PricePerUnit { get; set; }

    public decimal SecurityDeposit { get; set; }

    public decimal CleaningCharge { get; set; }

    public decimal AdditionalCharge { get; set; }

    /// <summary>false = direct booking goes straight to Approved; true = a
    /// booking starts Pending and an admin must approve/reject it.</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>How many days ahead a resident may book (0 = no limit).</summary>
    public int AdvanceBookingDaysLimit { get; set; }

    /// <summary>Minimum hours before the booking's start time that a
    /// resident may still cancel it themselves (0 = no restriction).</summary>
    public int CancellationHoursBeforeStart { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<FacilityBlackoutDate> BlackoutDates { get; set; } = new List<FacilityBlackoutDate>();

    public ICollection<FacilityBooking> Bookings { get; set; } = new List<FacilityBooking>();
}
