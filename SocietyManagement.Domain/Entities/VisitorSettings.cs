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
}
