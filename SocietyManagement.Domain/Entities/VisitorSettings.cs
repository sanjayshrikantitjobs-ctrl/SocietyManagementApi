using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One row per society, mirrors MaintenanceSettings' shape.
/// Reminder/QR/frequent-visitor toggles (spec section 40) get added to
/// this same table in later phases rather than a new table each time.</summary>
public class VisitorSettings : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int ApprovalRequestExpiryMinutes { get; set; } = 30;

    /// <summary>How long a visitor's gate-entry history (and their reusable
    /// Visitor/photo record, once it has no visit left within this window)
    /// is kept before VisitorDataRetentionService hard-deletes it — from
    /// both the database and the photo's blob/disk storage.</summary>
    public int RetentionDays { get; set; } = 30;
}
