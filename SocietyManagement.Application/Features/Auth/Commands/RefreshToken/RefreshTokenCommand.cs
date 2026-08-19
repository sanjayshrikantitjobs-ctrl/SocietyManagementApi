using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Auth.Common;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Auth.Commands.RefreshToken;

/// <summary>Rotates a refresh token: the presented token is revoked and replaced,
/// a brand-new access token is issued. If a caller ever presents a token that is
/// already revoked, the whole chain is revoked (reuse-detection).</summary>
public record RefreshTokenCommand(string AccessToken, string RefreshToken, string? IpAddress)
    : IRequest<LoginResponseDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTime _dateTime;

    public RefreshTokenCommandHandler(IApplicationDbContext context, IJwtService jwtService, IDateTime dateTime)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTime = dateTime;
    }

    public async Task<LoginResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.RefreshToken, out var tokenGuid))
        {
            throw new UnauthorizedAppException("Invalid refresh token.");
        }

        var existingToken = await _context.RefreshTokens
            .Include(rt => rt.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.Token == tokenGuid, cancellationToken);

        if (existingToken is null)
        {
            throw new UnauthorizedAppException("Invalid refresh token.");
        }

        if (existingToken.RevokedAt is not null)
        {
            // Token reuse detected: revoke every active token for this user.
            var allActive = await _context.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var t in allActive)
            {
                t.RevokedAt = _dateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("Refresh token has already been used. Please log in again.");
        }

        if (existingToken.IsExpired)
        {
            throw new UnauthorizedAppException("Refresh token has expired. Please log in again.");
        }

        var user = existingToken.User;
        if (!user.IsActive || user.IsDeleted)
        {
            throw new UnauthorizedAppException("Account is inactive.");
        }

        var newToken = new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            ExpiresAt = _dateTime.UtcNow.AddDays(AppConstants.RefreshTokenExpiryDays),
            CreatedByIp = request.IpAddress
        };

        existingToken.RevokedAt = _dateTime.UtcNow;
        existingToken.RevokedByIp = request.IpAddress;
        existingToken.ReplacedByToken = newToken.Token;

        await _context.RefreshTokens.AddAsync(newToken, cancellationToken);

        var permissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId)
            .Select(rp => rp.Permission.Code)
            .ToListAsync(cancellationToken);

        var accessToken = _jwtService.GenerateAccessToken(user, permissions);

        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newToken.Token.ToString(),
            AccessTokenExpiresAt = _dateTime.UtcNow.AddMinutes(AppConstants.AccessTokenExpiryMinutes),
            User = new UserProfileDto
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
            }
        };
    }
}
