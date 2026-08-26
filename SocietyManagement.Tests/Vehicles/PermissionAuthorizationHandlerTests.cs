using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SocietyManagement.API.Authorization;
using SocietyManagement.Shared.Constants;
using Xunit;

namespace SocietyManagement.Tests.Vehicles;

/// <summary>Covers "[HasPermission] actually blocks a caller without the
/// permission" at the level that's actually testable without standing up a
/// full HTTP pipeline: the same PermissionAuthorizationHandler every
/// [HasPermission(...)]-decorated action (including VehicleScansController's
/// four new actions) is authorized through.</summary>
public class PermissionAuthorizationHandlerTests
{
    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(string requiredPermission, params string[] grantedPermissions)
    {
        var claims = grantedPermissions.Select(p => new Claim("perm", p));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new PermissionRequirement(requiredPermission);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await new PermissionAuthorizationHandler().HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Watchman_WithVehiclesScan_IsAuthorizedForScan()
    {
        var context = await AuthorizeAsync(Permissions.Vehicles.Scan, Permissions.Vehicles.Scan, Permissions.Vehicles.Search);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task MemberRole_WithoutVehiclesPermissions_IsBlockedFromScan()
    {
        // A Member login only ever holds the member-self-service permission
        // set (see DbSeeder.cs's memberPermissionCodes) — none of the four
        // new Vehicles.* codes are in it.
        var context = await AuthorizeAsync(Permissions.Vehicles.Scan, Permissions.Members.View, Permissions.Complaints.Create);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Watchman_WithoutViewOwnerDetails_IsBlockedFromThatSpecificPermission()
    {
        // Confirms Scan+Search don't implicitly grant ViewOwnerDetails —
        // the PII-redaction gate inside ConfirmVehicleScanCommandHandler
        // relies on this being a genuinely separate, unheld permission.
        var context = await AuthorizeAsync(
            Permissions.Vehicles.ViewOwnerDetails, Permissions.Vehicles.Scan, Permissions.Vehicles.Search);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task NoClaimsAtAll_IsBlocked()
    {
        var context = await AuthorizeAsync(Permissions.Vehicles.Scan);
        Assert.False(context.HasSucceeded);
    }
}
