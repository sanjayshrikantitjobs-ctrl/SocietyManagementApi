using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class Flat : BaseAuditableEntity
{
    public int FloorId { get; set; }
    public Floor Floor { get; set; } = default!;

    public string FlatNumber { get; set; } = default!;

    public FlatType FlatType { get; set; }

    public decimal? AreaSqFt { get; set; }

    public FlatStatus Status { get; set; } = FlatStatus.Vacant;

    /// <summary>Owner contact fields used by Maintenance billing (invoice "Owner
    /// Name", WhatsApp delivery number) as a fallback when no current
    /// FlatResidency exists yet for this flat — the real source of truth is
    /// FlatResidency -> Member once residents are on file (see Features/Residents).</summary>
    public string? OwnerName { get; set; }

    public string? OwnerPhone { get; set; }

    public string? OwnerEmail { get; set; }

    public ICollection<ParkingSlot> ParkingSlots { get; set; } = new List<ParkingSlot>();
    public ICollection<FlatResidency> Residencies { get; set; } = new List<FlatResidency>();
}
