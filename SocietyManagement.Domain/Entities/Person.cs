using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A reusable person record backing the Owner/Tenant Occupancy
/// model — deliberately independent of the older Member/FlatResidency
/// model (see Features/Residents), which stays untouched and keeps serving
/// Vehicles/EmergencyContacts/EventRsvp/etc. A Person has no login concept
/// (no UserId): Member already owns that relationship.</summary>
public class Person : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public string Phone { get; set; } = default!;

    public string? Email { get; set; }

    public Gender? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? PhotoUrl { get; set; }

    /// <summary>Identity document, optional, not a dedup key — a person can
    /// legitimately share these with no other record.</summary>
    public string? AadhaarNumber { get; set; }

    public string? PanNumber { get; set; }

    public ICollection<OccupancyMember> OccupancyMemberships { get; set; } = new List<OccupancyMember>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
