using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class Asset : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;

    public AssetCategory Category { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int TotalQuantity { get; set; }

    public AssetPricingType PricingType { get; set; }

    public decimal RentalPrice { get; set; }

    /// <summary>Per-unit deposit — multiplied by the requested quantity when
    /// a rental is priced (see AssetBookingFeature's price calculation).</summary>
    public decimal SecurityDeposit { get; set; }

    /// <summary>Per-unit charge applied when a returned item is marked damaged.</summary>
    public decimal DamageCharge { get; set; }

    /// <summary>Per-day-late, per-unit charge applied on an overdue return.</summary>
    public decimal LateReturnCharge { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<AssetBookingItem> BookingItems { get; set; } = new List<AssetBookingItem>();
}
