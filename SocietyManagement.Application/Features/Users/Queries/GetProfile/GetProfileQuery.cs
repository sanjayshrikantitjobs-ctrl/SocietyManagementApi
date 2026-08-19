using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Auth.Common;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Users.Queries.GetProfile;

/// <summary>"Profile Management" — returns the currently authenticated user's
/// own profile plus effective permissions, used to rehydrate Angular state on
/// page refresh (GET /api/auth/me).</summary>
public record GetProfileQuery : IRequest<UserProfileDto>;

public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, UserProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetProfileQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UserProfileDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), _currentUser.UserId ?? 0);

        var permissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId)
            .Select(rp => rp.Permission.Code)
            .ToListAsync(cancellationToken);

        return new UserProfileDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            MobileNumber = user.MobileNumber,
            ProfilePhotoUrl = user.ProfilePhotoUrl,
            RoleName = user.Role.Name,
            Permissions = permissions,
            MustChangePassword = user.MustChangePassword
        };
    }
}
