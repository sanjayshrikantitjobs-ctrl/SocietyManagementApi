using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One rental request, covering one or more Assets (AssetBookingItem)
/// for the same date/duration — mirrors the web spec's "resident picks
/// date+asset+quantity+duration, can add multiple assets" flow.</summary>
public class AssetBooking : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public int RequestedByUserId { get; set; }

    public string RequestedByName { get; set; } = default!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? Notes { get; set; }

    public decimal RentalCharge { get; set; }

    public decimal SecurityDepositAmount { get; set; }

    public decimal DamageChargeAmount { get; set; }

    public decimal LateChargeAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DepositRefundAmount { get; set; }

    public AssetBookingStatus Status { get; set; } = AssetBookingStatus.Pending;

    public string? RejectionReason { get; set; }

    /// <summary>Optional link to a FacilityBooking for the same event.</summary>
    public int? FacilityBookingId { get; set; }
    public FacilityBooking? FacilityBooking { get; set; }

    public ICollection<AssetBookingItem> Items { get; set; } = new List<AssetBookingItem>();
}
