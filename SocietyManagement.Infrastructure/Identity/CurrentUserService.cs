using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? RoleName => User?.FindFirstValue(ClaimTypes.Role);

    public int? SocietyId
    {
        get
        {
            var value = User?.FindFirstValue("society_id");
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Permissions =>
        User?.FindAll("perm").Select(c => c.Value).ToList() ?? new List<string>();

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
}
