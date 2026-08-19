using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Users.Commands.CreateUser;

/// <summary>Admin-only: "Admin can Create User, Assign Role" per spec. A random
/// temporary password is generated and MustChangePassword is set so the new user
/// is forced to set their own password on first login.</summary>
public record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string MobileNumber,
    int RoleId) : IRequest<int>;

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
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;

    public CreateUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IAuditService auditService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _auditService = auditService;
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

        if (!await _context.Roles.AnyAsync(r => r.Id == request.RoleId && !r.IsDeleted, cancellationToken))
        {
            throw new NotFoundException(nameof(Role), request.RoleId);
        }

        var temporaryPassword = GenerateTemporaryPassword();

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            MobileNumber = request.MobileNumber,
            RoleId = request.RoleId,
            PasswordHash = _passwordHasher.Hash(temporaryPassword),
            MustChangePassword = true,
            IsActive = true
        };

        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _emailService.SendEmailAsync(
            user.Email,
            "Your Society Management account has been created",
            $"<p>Hello {user.FirstName},</p>" +
            $"<p>An account has been created for you. Your temporary password is: <b>{temporaryPassword}</b></p>" +
            "<p>You will be asked to set a new password on first login.</p>",
            cancellationToken);

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
