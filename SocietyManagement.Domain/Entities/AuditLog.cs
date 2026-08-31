using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>
/// Immutable, append-only trail (no BaseAuditableEntity/soft-delete here on purpose
/// — audit rows must never be edited or hidden). Written by
/// AuditLoggingBehaviour/AuditableEntitySaveChangesInterceptor and read by the
/// Audit Logs screen.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    /// <summary>Null for a Super Admin action (no tenant boundary — matches
    /// ICurrentUserService.SocietyId's own nullability), populated for every
    /// regular user's action. Added so a per-society Audit Logs screen
    /// doesn't need a full-table scan once many societies share this table.</summary>
    public int? SocietyId { get; set; }

    public AuditAction Action { get; set; }

    public string Module { get; set; } = default!;

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public string? OldValues { get; set; } // JSON snapshot

    public string? NewValues { get; set; } // JSON snapshot

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
