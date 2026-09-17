using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One row per (Announcement, User) that user has bookmarked for
/// later — mirrors AnnouncementRead, but toggled both ways (see
/// ToggleAnnouncementSavedCommand) rather than write-once.</summary>
public class AnnouncementSave : BaseEntity
{
    public int AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = default!;

    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime SavedAt { get; set; }
}
