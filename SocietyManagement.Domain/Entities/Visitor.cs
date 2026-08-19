using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>A person, independent of any single visit — reused across
/// repeat visits and, in later phases, frequent-visitor and domestic-help
/// records without duplicating name/mobile/photo fields.</summary>
public class Visitor : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string MobileNumber { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public string? VehicleNumber { get; set; }
    public string? VehicleType { get; set; }
    public string? IdType { get; set; }
    public string? IdReference { get; set; }
    public string? Notes { get; set; }

    public ICollection<VisitorVisit> Visits { get; set; } = new List<VisitorVisit>();
}
