using AutoMapper;
using SocietyManagement.Application.Common.Mappings;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Application.Features.Users.Common;

public class UserDto : IMapFrom<User>
{
    public int Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string MobileNumber { get; set; } = default!;
    public string? ProfilePhotoUrl { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public void Mapping(Profile profile) =>
        profile.CreateMap<User, UserDto>()
            .ForMember(d => d.RoleName, o => o.MapFrom(s => s.Role.Name));
}
