using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>
/// A role groups a set of permissions. Ships with two system roles (Admin, Member)
/// but is fully data-driven so new roles can be created from the Role Management
/// screen without a code change or deployment (see spec: "Future-ready role system").
/// </summary>
public class Role : BaseAuditableEntity
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>System roles (Admin/Member) cannot be renamed or deleted from the UI.</summary>
    public bool IsSystemRole { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
