using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Users.Commands.DeleteUser;

/// <summary>Soft delete only — IsDeleted=true, row stays for audit/history. See
/// ApplicationDbContext global query filter and AuditableEntitySaveChangesInterceptor.</summary>
public record DeleteUserCommand(int Id) : IRequest<Unit>;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public DeleteUserCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IAuditService auditService)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        // Uncovered by SocietyScopeFilter (bound parameter is "Id", not
        // "SocietyId") — without this, a scoped Admin could delete any
        // OTHER society's user by guessing an id. Super Admin is unrestricted.
        if (_currentUser.SocietyId.HasValue && _currentUser.SocietyId != user.SocietyId)
        {
            throw new ForbiddenAccessException("You can only manage users in your own society.");
        }

        if (user.Id == _currentUser.UserId)
        {
            throw new BadRequestAppException("You cannot delete your own account.");
        }

        user.IsDeleted = true;
        user.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Delete, "Users", nameof(User), user.Id.ToString(),
            ct: cancellationToken);

        return Unit.Value;
    }
}
