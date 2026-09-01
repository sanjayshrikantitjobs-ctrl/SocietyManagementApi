using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>Watchman-created record of a parking violation, evidenced by an
/// optional photo. "Removed" = IsDeleted, via the standard soft-delete
/// mechanism — no separate billing/status lifecycle like FineRecord (Phase 1
/// has no billing integration for this). SocietyId is stored directly since
/// Vehicle has no SocietyId of its own — matches VehicleScanLog/ParkingSlot.</summary>
public class ParkingFine : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = default!;

    /// <summary>Set only when Reason == WrongAllottedSlot — the slot the
    /// vehicle was actually found in (not the one it's allotted to).</summary>
    public int? ParkingSlotId { get; set; }
    public ParkingSlot? ParkingSlot { get; set; }

    public ParkingFineReason Reason { get; set; }

    public string? Notes { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Evidence photo — optional, never blocks recording a fine
    /// (camera unavailable/broken shouldn't stop a Watchman from acting).</summary>
    public string? PhotoUrl { get; set; }

    public DateTime FineDate { get; set; }

    public int IssuedByUserId { get; set; }
    public User IssuedByUser { get; set; } = default!;
}
