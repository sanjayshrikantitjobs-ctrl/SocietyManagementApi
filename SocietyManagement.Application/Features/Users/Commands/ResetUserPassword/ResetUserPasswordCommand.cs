using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Users.Commands.ResetUserPassword;

/// <summary>Admin action — spec: "Admin can Reset Password". Generates a new
/// temporary password, emails it, and forces a change on next login. Unlike
/// ChangePasswordCommand this does not require knowing the old password.</summary>
public record ResetUserPasswordCommand(int Id) : IRequest<Unit>;

public class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;

    public ResetUserPasswordCommandHandler(
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

    public async Task<Unit> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        var temporaryPassword = Guid.NewGuid().ToString("N")[..10] + "!A1";
        user.PasswordHash = _passwordHasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.IsLocked = false;
        user.LockedUntil = null;
        user.AccessFailedCount = 0;

        await _context.SaveChangesAsync(cancellationToken);

        await _emailService.SendEmailAsync(
            user.Email,
            "Your password has been reset",
            $"<p>Hello {user.FirstName},</p><p>Your temporary password is: <b>{temporaryPassword}</b></p>" +
            "<p>You will be asked to set a new password on next login.</p>",
            cancellationToken);

        await _auditService.LogAsync(AuditAction.PasswordReset, "Users", nameof(User), user.Id.ToString(),
            ct: cancellationToken);

        return Unit.Value;
    }
}
