using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class Vehicle : BaseAuditableEntity
{
    /// <summary>At least one of MemberId/FlatId must be set (enforced in the
    /// command handlers, not the DB). MemberId is the older Member-model
    /// assignment; FlatId is the newer Owner/Tenant Occupancy model's
    /// assignment — a flat can have vehicles even when no legacy Member
    /// record exists for its occupants.</summary>
    public int? MemberId { get; set; }
    public Member? Member { get; set; }

    public int? FlatId { get; set; }
    public Flat? Flat { get; set; }

    public VehicleType VehicleType { get; set; }

    public string RegistrationNumber { get; set; } = default!;

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? Color { get; set; }

    public int? ParkingSlotId { get; set; }
    public ParkingSlot? ParkingSlot { get; set; }
}
