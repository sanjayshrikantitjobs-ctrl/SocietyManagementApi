using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Users.Commands.LockUser;

/// <summary>Toggles Lock/Unlock — spec: "Admin can Lock Account".</summary>
public record LockUserCommand(int Id, bool Lock) : IRequest<Unit>;

public class LockUserCommandHandler : IRequestHandler<LockUserCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public LockUserCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        user.IsLocked = request.Lock;
        user.LockedUntil = request.Lock ? DateTime.UtcNow.AddYears(100) : null;
        if (!request.Lock)
        {
            user.AccessFailedCount = 0;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            request.Lock ? AuditAction.AccountLocked : AuditAction.AccountUnlocked,
            "Users", nameof(User), user.Id.ToString(), ct: cancellationToken);

        return Unit.Value;
    }
}
