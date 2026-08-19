using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>
/// Opaque, rotating refresh token. On refresh, the old token is marked
/// Revoked/ReplacedByToken rather than deleted, so token-reuse (a stolen token
/// being replayed after rotation) can be detected and the whole chain revoked.
/// </summary>
public class RefreshToken : BaseAuditableEntity
{
    public Guid Token { get; set; } = Guid.NewGuid();

    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? ReplacedByToken { get; set; }

    public string? CreatedByIp { get; set; }

    public string? RevokedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => RevokedAt is null && !IsExpired;
}
