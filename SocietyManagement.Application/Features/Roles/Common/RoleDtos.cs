using AutoMapper;
using SocietyManagement.Application.Common.Mappings;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Application.Features.Roles.Common;

public class RoleDto : IMapFrom<Role>
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int UserCount { get; set; }

    public void Mapping(Profile profile) =>
        profile.CreateMap<Role, RoleDto>()
            .ForMember(d => d.UserCount, o => o.MapFrom(s => s.Users.Count(u => !u.IsDeleted)));
}

public class RoleDetailDto : IMapFrom<Role>
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public List<int> PermissionIds { get; set; } = new();

    public void Mapping(Profile profile) =>
        profile.CreateMap<Role, RoleDetailDto>()
            .ForMember(d => d.PermissionIds,
                o => o.MapFrom(s => s.RolePermissions.Select(rp => rp.PermissionId)));
}

public class PermissionDto : IMapFrom<Permission>
{
    public int Id { get; set; }
    public string Module { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Description { get; set; }
}
