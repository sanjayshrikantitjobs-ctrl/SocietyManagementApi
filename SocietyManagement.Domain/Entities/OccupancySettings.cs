using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One row per society — Owner/Tenant Occupancy module config.
/// Mirrors MaintenanceSettings/VisitorSettings's one-row-per-society shape.</summary>
public class OccupancySettings : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    /// <summary>False (default) = enforce "one Primary Owner per flat".
    /// True = allow more than one current Owner OccupancyMember with
    /// IsPrimary=true on the same flat (e.g. co-owners with equal standing).</summary>
    public bool AllowMultiplePrimaryOwners { get; set; }
}
