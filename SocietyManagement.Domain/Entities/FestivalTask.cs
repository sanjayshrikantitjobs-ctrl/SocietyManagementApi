using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One coordination item ("Coordinate Panditji for Puja") for a
/// festival, optionally assigned to a volunteer. Feeds the Dashboard tab's
/// "Tasks Pending" KPI (Status != Completed).</summary>
public class FestivalTask : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Null = unassigned. SetNull on the volunteer's own delete —
    /// removing a volunteer shouldn't cascade-delete their tasks.</summary>
    public int? AssignedVolunteerId { get; set; }
    public FestivalVolunteer? AssignedVolunteer { get; set; }

    public FestivalTaskStatus Status { get; set; } = FestivalTaskStatus.Pending;

    public DateTime? DueDate { get; set; }
}
