using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>A festival's volunteer roster — a plain contact, not tied to
/// Person/Member (a volunteer needn't even be a resident, e.g. a hired
/// helper), and deliberately separate from FestivalTask.Responsibility so
/// one volunteer can be assigned any number of tasks.</summary>
public class FestivalVolunteer : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Notes { get; set; }

    public ICollection<FestivalTask> Tasks { get; set; } = new List<FestivalTask>();
}
