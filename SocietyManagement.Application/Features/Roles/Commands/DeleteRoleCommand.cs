using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Roles.Commands;

public record DeleteRoleCommand(int Id) : IRequest<Unit>;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public DeleteRoleCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.Id);

        if (role.IsSystemRole)
        {
            throw new BadRequestAppException("System roles (Admin/Member) cannot be deleted.");
        }

        if (await _context.Users.AnyAsync(u => u.RoleId == role.Id && !u.IsDeleted, cancellationToken))
        {
            throw new ConflictAppException("Cannot delete a role that is still assigned to users.");
        }

        role.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Delete, "Roles", nameof(Role), role.Id.ToString(),
            ct: cancellationToken);

        return Unit.Value;
    }
}
