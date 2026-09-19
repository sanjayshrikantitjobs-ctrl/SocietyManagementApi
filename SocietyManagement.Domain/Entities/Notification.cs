using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One persisted, per-recipient notification — the durable half of
/// NotificationService's SignalR push. Unlike AnnouncementRead (a join row
/// on a shared entity many users see), a Notification is already fanned out
/// per-recipient at creation time, so IsRead lives directly here rather than
/// in a separate per-user state table.</summary>
public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>The exact SignalR event-name string already used by
    /// NotificationService (e.g. "ComplaintRaised") — one taxonomy, not two.</summary>
    public string EventType { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;

    /// <summary>The same payload object already sent over SignalR,
    /// serialized — carries whatever entity IDs the client needs to deep-link
    /// (e.g. ComplaintId, AnnouncementId), with no separate deep-link DTO.</summary>
    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Optional caller-supplied stable key (e.g. "complaint-42-raised")
    /// — when supplied, NotificationService checks (UserId, EventType,
    /// DedupeKey) before inserting so a retried command/duplicate event
    /// delivery can't create a second row for the same (recipient, event).
    /// Null when a call site has no natural key; no uniqueness is enforced
    /// for those (matches today's fire-once-per-call behavior).</summary>
    public string? DedupeKey { get; set; }
}
