using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A person registered against the society — owner, tenant, or
/// family member. Independent of any single flat; the relationship (role,
/// move-in/out dates) lives on FlatResidency, since one person can relate
/// to a flat in different ways over time and a flat can have several
/// current residents (joint owners, a tenant's family, ...).</summary>
public class Member : BaseAuditableEntity
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

    /// <summary>Set once this member is given login access.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<FlatResidency> Residencies { get; set; } = new List<FlatResidency>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
