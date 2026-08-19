using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Roles.Commands;

/// <summary>Updates name/description and fully replaces the permission set —
/// this is how "Permissions should be stored in database" stays editable from the UI.</summary>
public record UpdateRoleCommand(int Id, string Name, string? Description, List<int> PermissionIds)
    : IRequest<Unit>;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public UpdateRoleCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.Id);

        if (role.IsSystemRole && role.Name != request.Name)
        {
            throw new BadRequestAppException("System roles (Admin/Member) cannot be renamed.");
        }

        role.Name = request.Name;
        role.Description = request.Description;

        var existingPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        var newPermissionIds = request.PermissionIds.ToHashSet();

        var toRemove = role.RolePermissions.Where(rp => !newPermissionIds.Contains(rp.PermissionId)).ToList();
        foreach (var rp in toRemove)
        {
            role.RolePermissions.Remove(rp);
        }

        var toAdd = newPermissionIds.Except(existingPermissionIds);
        foreach (var permissionId in toAdd)
        {
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Roles", nameof(Role), role.Id.ToString(),
            ct: cancellationToken);

        return Unit.Value;
    }
}
