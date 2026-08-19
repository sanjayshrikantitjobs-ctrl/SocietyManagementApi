using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A dated, capacity-limited happening (dinner, AGM, sports day, ...).
/// Optionally funded by / associated with a Festival, but stands on its own
/// so it's reusable beyond festival-funded gatherings.</summary>
public class Event : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    /// <summary>Null for a standalone society event not tied to any festival.</summary>
    public int? FestivalId { get; set; }
    public Festival? Festival { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime EventDateTime { get; set; }
    public string? Venue { get; set; }

    /// <summary>Null = unlimited attendance.</summary>
    public int? CapacityLimit { get; set; }

    public DateTime? RsvpDeadline { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public ICollection<EventRsvp> Rsvps { get; set; } = new List<EventRsvp>();
}
