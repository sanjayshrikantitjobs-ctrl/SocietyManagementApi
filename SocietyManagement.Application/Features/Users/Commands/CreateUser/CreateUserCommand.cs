using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Users.Commands.CreateUser;

/// <summary>Admin-only: "Admin can Create User, Assign Role" per spec. If
/// <see cref="Password"/> is left blank a random temporary password is generated
/// and MustChangePassword is set so the new user is forced to set their own
/// password on first login; if the admin supplies one directly it's used as-is
/// and MustChangePassword is left false, since the admin already knows it and
/// will hand it to the user out of band.
///
/// <see cref="SocietyId"/> is only meaningful for a Super Admin caller —
/// creating anything for a specific society requires picking one; a scoped
/// Admin's own SocietyId is always used instead and any client-supplied
/// value here is ignored for them (never trust client input for the tenant
/// boundary). Creating a SuperAdmin- or Admin-role user is Super-Admin-only.</summary>
public record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string MobileNumber,
    int RoleId,
    string? Password = null,
    int? SocietyId = null) : IRequest<int>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().Must(e => e.IsValidEmail()).WithMessage("A valid email is required.");
        RuleFor(x => x.MobileNumber).NotEmpty().Must(m => m.IsValidIndianMobile())
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.RoleId).GreaterThan(0);
        RuleFor(x => x.Password)
            .MinimumLength(8).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a special character.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public CreateUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken))
        {
            throw new ConflictAppException("A user with this email already exists.");
        }

        if (await _context.Users.AnyAsync(u => u.MobileNumber == request.MobileNumber && !u.IsDeleted,
                cancellationToken))
        {
            throw new ConflictAppException("A user with this mobile number already exists.");
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        var callerIsSuperAdmin = _currentUserService.SocietyId is null;
        int? resolvedSocietyId;

        if (role.Name == Shared.Constants.Roles.SuperAdmin)
        {
            if (!callerIsSuperAdmin)
            {
                throw new ForbiddenAccessException("Only a Super Admin can create another Super Admin.");
            }
            resolvedSocietyId = null;
        }
        else if (role.Name == Shared.Constants.Roles.Admin)
        {
            if (!callerIsSuperAdmin)
            {
                throw new ForbiddenAccessException("Only a Super Admin can create an Admin account.");
            }
            if (!request.SocietyId.HasValue)
            {
                throw new BadRequestAppException("A society must be selected to create an Admin for it.");
            }
            if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, cancellationToken))
            {
                throw new NotFoundException(nameof(Society), request.SocietyId.Value);
            }
            resolvedSocietyId = request.SocietyId;
        }
        else
        {
            // Member/Watchman: a scoped Admin's own society is always used,
            // never the client-supplied value — only Super Admin's explicit
            // choice is trusted, since they have no implicit society of their own.
            resolvedSocietyId = callerIsSuperAdmin ? request.SocietyId : _currentUserService.SocietyId;
            if (!resolvedSocietyId.HasValue)
            {
                throw new BadRequestAppException("A society must be selected to create this user for it.");
            }
        }

        var adminSetPassword = !string.IsNullOrEmpty(request.Password);
        var password = adminSetPassword ? request.Password! : GenerateTemporaryPassword();

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            MobileNumber = request.MobileNumber,
            RoleId = request.RoleId,
            SocietyId = resolvedSocietyId,
            PasswordHash = _passwordHasher.Hash(password),
            MustChangePassword = !adminSetPassword,
            IsActive = true
        };

        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // When the admin sets the password directly they already know it and will
        // hand it to the user out of band — only email the generated temp password.
        if (!adminSetPassword)
        {
            await _emailService.SendEmailAsync(
                user.Email,
                "Your Society Management account has been created",
                $"<p>Hello {user.FirstName},</p>" +
                $"<p>An account has been created for you. Your temporary password is: <b>{password}</b></p>" +
                "<p>You will be asked to set a new password on first login.</p>",
                cancellationToken);
        }

        await _auditService.LogAsync(Domain.Enums.AuditAction.Create, "Users", nameof(User), user.Id.ToString(),
            newValues: new { user.Email, user.MobileNumber, user.RoleId }, ct: cancellationToken);

        return user.Id;
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
        var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
        var result = new char[12];
        for (var i = 0; i < 12; i++)
        {
            result[i] = chars[random[i] % chars.Length];
        }
        return new string(result);
    }
}
