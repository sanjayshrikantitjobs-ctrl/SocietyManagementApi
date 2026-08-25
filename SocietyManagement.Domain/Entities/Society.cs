using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>Root of the Society Setup hierarchy: Society -> Building -> Wing -> Floor -> Flat.</summary>
public class Society : BaseAuditableEntity
{
    public string Name { get; set; } = default!;

    /// <summary>Short unique code a non-Super-Admin login must supply
    /// alongside identifier+password, so it must match the caller's own
    /// User.SocietyId at login time. Server-generated on create; nullable
    /// because pre-existing societies get one via a startup backfill, not
    /// at insert time.</summary>
    public string? Code { get; set; }

    public string? RegistrationNumber { get; set; }

    public string Address { get; set; } = default!;

    public string City { get; set; } = default!;

    public string State { get; set; } = default!;

    public string Pincode { get; set; } = default!;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? LogoUrl { get; set; }

    public DateTime? EstablishedDate { get; set; }

    public ICollection<Building> Buildings { get; set; } = new List<Building>();

    public ICollection<ParkingSlot> ParkingSlots { get; set; } = new List<ParkingSlot>();
}
