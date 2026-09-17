using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One asset line within an AssetBooking — quantity requested plus
/// the admin's physical issue/return/damage/loss tracking for that line.</summary>
public class AssetBookingItem : BaseEntity
{
    public int AssetBookingId { get; set; }
    public AssetBooking AssetBooking { get; set; } = default!;

    public int AssetId { get; set; }
    public Asset Asset { get; set; } = default!;

    public int Quantity { get; set; }

    /// <summary>Snapshot of Asset.RentalPrice at booking time, so a later
    /// price change never retroactively alters an existing rental's cost.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public int QuantityIssued { get; set; }

    public int QuantityReturned { get; set; }

    public int QuantityDamaged { get; set; }

    public int QuantityLost { get; set; }
}
