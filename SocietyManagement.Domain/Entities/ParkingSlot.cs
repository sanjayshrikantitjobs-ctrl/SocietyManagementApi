using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class ParkingSlot : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string SlotNumber { get; set; } = default!;

    public ParkingType Type { get; set; }

    public ParkingStatus Status { get; set; } = ParkingStatus.Vacant;

    public int? AllocatedFlatId { get; set; }
    public Flat? AllocatedFlat { get; set; }
}
