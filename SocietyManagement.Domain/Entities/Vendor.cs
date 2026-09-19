using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A society-level service vendor/contractor. Distinct from
/// FestivalVendor, which is scoped to one festival's budget.</summary>
public class Vendor : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;
    public ServiceVendorCategory Category { get; set; }
    public string? ContactPerson { get; set; }
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? GstNumber { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public string? PerformanceNotes { get; set; }
    public bool IsActive { get; set; } = true;
}
