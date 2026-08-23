using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One continuous Owner or Tenant occupation episode of a flat —
/// the group container for OccupancyMember rows. Never deleted once
/// closed; EndDate null = current episode for this Flat+Type. Closing an
/// episode (EndOccupancyCommand) closes every current member in it
/// together, which is what makes "a tenant's family moves out as one
/// unit" a property of the model, not app-code discipline an admin has to
/// remember.</summary>
public class FlatOccupancy : BaseAuditableEntity
{
    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public OccupancyType Type { get; set; }

    public DateTime StartDate { get; set; }

    /// <summary>Null = current episode. Deliberately not a stored
    /// IsCurrent bool — "current" is always EndDate == null, computed at
    /// query time, matching FlatOccupancySummaryDto's convention.</summary>
    public DateTime? EndDate { get; set; }

    public string? Notes { get; set; }

    public ICollection<OccupancyMember> Members { get; set; } = new List<OccupancyMember>();
    public RentalAgreement? RentalAgreement { get; set; }
}
