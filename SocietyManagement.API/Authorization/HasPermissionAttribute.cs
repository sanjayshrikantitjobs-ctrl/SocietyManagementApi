using Microsoft.AspNetCore.Authorization;

namespace SocietyManagement.API.Authorization;

/// <summary>
/// Usage: [HasPermission(Permissions.Members.Create)] on a controller action.
/// Implements IAuthorizationRequirementData (ASP.NET Core 8+) so the requirement
/// is generated per-attribute-instance — no need to pre-register one named
/// policy per permission code in Program.cs.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class HasPermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Permission { get; }

    public HasPermissionAttribute(string permission) => Permission = permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Permission);
    }
}
