using Microsoft.AspNetCore.Authorization;

namespace SocietyManagement.API.Authorization;

/// <summary>
/// Checks the "perm" claim baked into the JWT at login time (see
/// Infrastructure.Services.JwtService) against the permission code required by
/// [HasPermission("...")] on a controller action. Because permissions travel
/// inside the token, this check never hits the database — the dynamic
/// role/permission matrix is only re-read at login/refresh time.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("perm", requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
