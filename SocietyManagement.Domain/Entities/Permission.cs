using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>
/// A single grantable capability, e.g. Module="Members", Action="Create".
/// <see cref="Code"/> (e.g. "members.create") is what controllers/UI check against
/// and is unique. Seeded from SocietyManagement.Shared.Constants.Permissions so the
/// seed list and the compile-time constants used in [HasPermission] attributes can
/// never drift apart.
/// </summary>
public class Permission : BaseAuditableEntity
{
    public string Module { get; set; } = default!;

    public string Action { get; set; } = default!;

    public string Code { get; set; } = default!;

    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
