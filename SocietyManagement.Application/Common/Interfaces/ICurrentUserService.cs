namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Reads identity out of the current HttpContext's ClaimsPrincipal.
/// Implemented in Infrastructure so Application never touches HttpContext directly.</summary>
public interface ICurrentUserService
{
    int? UserId { get; }
    string? Email { get; }
    string? RoleName { get; }
    /// <summary>Null = Super Admin (no tenant boundary). Set = the one
    /// Society this caller is confined to.</summary>
    int? SocietyId { get; }
    IReadOnlyList<string> Permissions { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permissionCode);
}
