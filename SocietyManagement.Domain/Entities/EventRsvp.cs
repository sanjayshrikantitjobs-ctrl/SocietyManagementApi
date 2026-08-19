using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One flat's headcount registration for an Event. One per
/// (EventId, FlatId) — resubmitting updates this row rather than creating
/// a duplicate. QrToken is the opaque value the flat's QR code encodes;
/// the check-in endpoint looks the RSVP up by it.</summary>
public class EventRsvp : BaseAuditableEntity
{
    public int EventId { get; set; }
    public Event Event { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    /// <summary>The flat's primary contact who registered.</summary>
    public int MemberId { get; set; }
    public Member Member { get; set; } = default!;

    public int HeadCount { get; set; }

    public string QrToken { get; set; } = default!;

    public EventRsvpStatus Status { get; set; } = EventRsvpStatus.Registered;

    /// <summary>Actual arrived headcount, set at check-in — may differ from
    /// the registered HeadCount if fewer people show up.</summary>
    public int? CheckedInCount { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public int? CheckedInByUserId { get; set; }
    public User? CheckedInByUser { get; set; }
}
