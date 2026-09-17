using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class FacilityBooking : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public int BookedByUserId { get; set; }

    public string BookedByName { get; set; } = default!;

    /// <summary>Date-only — the calendar day being booked; StartTime/EndTime
    /// are the time-of-day window within that day.</summary>
    public DateTime BookingDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string? Purpose { get; set; }

    public int GuestCount { get; set; }

    public string? Notes { get; set; }

    public decimal RentalCharge { get; set; }

    public decimal SecurityDepositAmount { get; set; }

    public decimal CleaningChargeAmount { get; set; }

    public decimal AdditionalChargeAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DepositRefundAmount { get; set; }

    public FacilityPaymentStatus PaymentStatus { get; set; } = FacilityPaymentStatus.Pending;

    public FacilityBookingStatus Status { get; set; } = FacilityBookingStatus.Pending;

    public string? RejectionReason { get; set; }

    /// <summary>Optional link so a facility booking and an asset rental can
    /// share the same event — see AssetBooking.FacilityBookingId.</summary>
    public ICollection<AssetBooking> LinkedAssetBookings { get; set; } = new List<AssetBooking>();
}
