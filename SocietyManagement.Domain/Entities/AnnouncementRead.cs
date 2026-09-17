using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One row per (Announcement, User) once that user has opened it —
/// existence of a row means "read"; absence means "unread". Never updated,
/// only inserted once (see MarkAnnouncementReadCommand's idempotent upsert).</summary>
public class AnnouncementRead : BaseEntity
{
    public int AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = default!;

    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime ReadAt { get; set; }
}
