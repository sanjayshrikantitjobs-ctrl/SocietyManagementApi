using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Auth.Common;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Auth.Commands.Login;

/// <summary>Logs in with either an email address or a 10-digit mobile number
/// (spec: "Login using Email / Mobile Number") plus password. Every
/// non-Super-Admin login also requires the correct SocietyCode — checked
/// in the handler (not the validator) since "is it required" depends on
/// the resolved user's role, which isn't known until after the DB lookup.</summary>
public record LoginCommand(string Identifier, string Password, string? IpAddress, string? SocietyCode = null) : IRequest<LoginResponseDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("Email or mobile number is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IDateTime _dateTime;
    private readonly IAuditService _auditService;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IDateTime dateTime,
        IAuditService auditService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _dateTime = dateTime;
        _auditService = auditService;
    }

    public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        // Only route to a MobileNumber lookup when the identifier actually
        // looks like one — everything else (a real email, or a synthetic
        // "{flatNumber}_{firstName}" login username, which is stored in the
        // Email column but contains no '@') goes to Email. The previous
        // IsEmailFormat() (Contains('@')) check sent non-email, non-mobile
        // identifiers down the MobileNumber branch, so no flat-owner/tenant
        // login created via CreateLoginForPersonAsync could ever sign in.
        var user = identifier.IsValidIndianMobile()
            ? await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.MobileNumber == identifier && !u.IsDeleted, cancellationToken)
            : await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == identifier && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAppException("Invalid email/mobile number or password.");
        }

        if (user.IsLocked && user.LockedUntil.HasValue && user.LockedUntil > _dateTime.UtcNow)
        {
            throw new UnauthorizedAppException(
                $"Account is locked until {user.LockedUntil:g}. Try again later or contact an administrator.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAppException("Account is inactive. Contact an administrator.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= AppConstants.MaxFailedLoginAttempts)
            {
                user.IsLocked = true;
                user.LockedUntil = _dateTime.UtcNow.AddMinutes(AppConstants.AccountLockoutMinutes);
                await _auditService.LogAsync(AuditAction.AccountLocked, "Auth", nameof(User), user.Id.ToString(),
                    ct: cancellationToken);
            }
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("Invalid email/mobile number or password.");
        }

        // Society Code gate — every non-Super-Admin login needs one. Checked
        // only after the password succeeds, so a wrong code never reveals
        // whether the identifier/password were otherwise correct.
        if (user.Role.Name != SocietyManagement.Shared.Constants.Roles.SuperAdmin)
        {
            if (user.SocietyId is null)
            {
                throw new UnauthorizedAppException("Your account is not linked to a society yet. Contact an administrator.");
            }
            if (string.IsNullOrWhiteSpace(request.SocietyCode))
            {
                throw new UnauthorizedAppException("Society code is required.");
            }

            var society = await _context.Societies
                .FirstOrDefaultAsync(s => s.Code == request.SocietyCode.Trim() && !s.IsDeleted, cancellationToken);
            if (society is null || society.Id != user.SocietyId)
            {
                throw new UnauthorizedAppException("Invalid society code.");
            }
        }

        // Successful login resets the failed-attempt counter.
        user.AccessFailedCount = 0;
        user.IsLocked = false;
        user.LockedUntil = null;
        user.LastLoginAt = _dateTime.UtcNow;
        user.LastLoginIp = request.IpAddress;

        var permissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId)
            .Select(rp => rp.Permission.Code)
            .ToListAsync(cancellationToken);

        var accessToken = _jwtService.GenerateAccessToken(user, permissions);

        // Fully qualified: this file's namespace has a sibling
        // Features.Auth.Commands.RefreshToken (the RefreshToken command feature),
        // which C# simple-name lookup resolves ahead of the `using`-imported
        // Domain.Entities.RefreshToken type, so an unqualified `new RefreshToken`
        // here binds to the namespace instead and fails to compile.
        var refreshToken = new SocietyManagement.Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            ExpiresAt = _dateTime.UtcNow.AddDays(AppConstants.RefreshTokenExpiryDays),
            CreatedByIp = request.IpAddress
        };
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(AuditAction.Login, "Auth", nameof(User), user.Id.ToString(),
            ct: cancellationToken);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token.ToString(),
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
                SocietyId = user.SocietyId,
                Permissions = permissions,
                MustChangePassword = user.MustChangePassword
            }
        };
    }
}
