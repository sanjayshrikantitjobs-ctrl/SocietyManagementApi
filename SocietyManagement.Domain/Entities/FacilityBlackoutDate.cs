using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>A whole day an admin has blocked out for a facility (maintenance,
/// festival setup, ...) — no booking may overlap this date regardless of time.</summary>
public class FacilityBlackoutDate : BaseEntity
{
    public int FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public DateTime BlackoutDate { get; set; }

    public string? Reason { get; set; }
}
