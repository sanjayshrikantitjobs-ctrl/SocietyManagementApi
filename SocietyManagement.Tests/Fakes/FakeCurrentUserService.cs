using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Tests.Fakes;

public class FakeCurrentUserService : ICurrentUserService
{
    public int? UserId { get; set; }
    public string? Email { get; set; }
    public string? RoleName { get; set; }
    public int? SocietyId { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public string? IpAddress { get; set; }
    public bool IsAuthenticated { get; set; } = true;

    public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
}
