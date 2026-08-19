using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class Vehicle : BaseAuditableEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = default!;

    public VehicleType VehicleType { get; set; }

    public string RegistrationNumber { get; set; } = default!;

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? Color { get; set; }

    public int? ParkingSlotId { get; set; }
    public ParkingSlot? ParkingSlot { get; set; }
}
