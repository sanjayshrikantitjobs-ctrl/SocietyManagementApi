using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>Join entity for the many-to-many Role &lt;-&gt; Permission relationship.</summary>
public class RolePermission : BaseAuditableEntity
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}
