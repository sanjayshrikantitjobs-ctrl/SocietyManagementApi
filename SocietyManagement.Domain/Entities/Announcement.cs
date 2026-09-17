using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class Announcement : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public AnnouncementType Type { get; set; }
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

    /// <summary>Uploaded via the existing POST /api/files/upload endpoint —
    /// stored here as the returned relative URL, same convention as every
    /// other image/attachment field in the app.</summary>
    public string? AttachmentUrl { get; set; }

    /// <summary>When Status is Scheduled, the time it should go live. Null
    /// when Status is Draft/Published directly (published immediately on
    /// creation) or Expired.</summary>
    public DateTime? PublishAt { get; set; }

    /// <summary>When it should stop showing to residents. Null = never
    /// expires on its own (an admin can still archive it manually via
    /// status, but nothing does it automatically).</summary>
    public DateTime? ExpiryAt { get; set; }

    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;

    public ICollection<AnnouncementRead> Reads { get; set; } = new List<AnnouncementRead>();
}
