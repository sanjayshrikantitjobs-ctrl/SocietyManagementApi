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
