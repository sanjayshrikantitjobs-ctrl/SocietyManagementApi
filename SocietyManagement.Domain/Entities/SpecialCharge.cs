using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A charge assigned to one specific flat only (Parking, Extra
/// Parking, Club House, Generator, Repair, custom charges) — separate from
/// the society-wide MaintenanceCategory lines every flat gets.</summary>
public class SpecialCharge : BaseAuditableEntity
{
    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public string ChargeName { get; set; } = default!;

    public decimal Amount { get; set; }

    public ChargeFrequency Frequency { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>OneTime charges are flipped to false automatically once billed
    /// once, so they don't repeat on the next generation run.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<MaintenanceBillItem> BillItems { get; set; } = new List<MaintenanceBillItem>();
}
