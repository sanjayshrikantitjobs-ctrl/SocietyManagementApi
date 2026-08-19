using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>
/// A system login account. Deliberately separate from Member: a User is
/// "someone who can log in", a Member is "a person registered against a
/// flat" (see Domain.Entities.Member). Not every User has a Member (e.g. the
/// bootstrap Admin) and not every Member has a User (most residents don't
/// need login access), hence the nullable FK.
/// </summary>
public class User : BaseAuditableEntity
{
    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string MobileNumber { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public string? ProfilePhotoUrl { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public int? MemberId { get; set; }
    public Member? Member { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLocked { get; set; }

    public int AccessFailedCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public bool EmailConfirmed { get; set; }

    public bool MobileConfirmed { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? LastLoginIp { get; set; }

    /// <summary>Forces a password change on next login (used after admin reset).</summary>
    public bool MustChangePassword { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
