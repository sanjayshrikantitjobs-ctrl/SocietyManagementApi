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
    private readonly ICurrentUserService _currentUserService;

    public LockUserCommandHandler(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        // Uncovered by SocietyScopeFilter (bound parameter is "Id", not
        // "SocietyId") — without this, a scoped Admin could lock/unlock any
        // OTHER society's user. Super Admin is unrestricted.
        if (_currentUserService.SocietyId.HasValue && _currentUserService.SocietyId != user.SocietyId)
        {
            throw new ForbiddenAccessException("You can only manage users in your own society.");
        }

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
