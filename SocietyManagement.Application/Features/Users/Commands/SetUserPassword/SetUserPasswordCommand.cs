using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Users.Commands.SetUserPassword;

/// <summary>Admin-typed counterpart to ResetUserPasswordCommand — for a
/// target whose email isn't reliably checked (e.g. a Watchman), the admin
/// picks the interim password directly instead of relying on delivery.
/// Still forces a change on next login, same as reset, so the admin-chosen
/// value is never the user's permanent password.</summary>
public record SetUserPasswordCommand(int Id, string NewPassword) : IRequest<Unit>;

public class SetUserPasswordCommandValidator : AbstractValidator<SetUserPasswordCommand>
{
    public SetUserPasswordCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NewPassword)
            .MinimumLength(8).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a special character.");
    }
}

public class SetUserPasswordCommandHandler : IRequestHandler<SetUserPasswordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public SetUserPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(SetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        // Same tenant check as ResetUserPasswordCommand — see its doc comment.
        if (_currentUserService.SocietyId.HasValue && _currentUserService.SocietyId != user.SocietyId)
        {
            throw new ForbiddenAccessException("You can only manage users in your own society.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = true;
        user.IsLocked = false;
        user.LockedUntil = null;
        user.AccessFailedCount = 0;

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(AuditAction.PasswordReset, "Users", nameof(User), user.Id.ToString(),
            ct: cancellationToken);

        return Unit.Value;
    }
}
