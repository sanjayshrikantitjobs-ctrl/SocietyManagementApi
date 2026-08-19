using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Roles.Commands;

/// <summary>Dynamic role creation — spec: "Future-ready role system... create
/// dynamic permission management." PermissionIds may be empty and assigned later.</summary>
public record CreateRoleCommand(string Name, string? Description, List<int> PermissionIds) : IRequest<int>;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public CreateRoleCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (await _context.Roles.AnyAsync(r => r.Name == request.Name && !r.IsDeleted, cancellationToken))
        {
            throw new ConflictAppException("A role with this name already exists.");
        }

        var role = new Role { Name = request.Name, Description = request.Description, IsSystemRole = false };
        await _context.Roles.AddAsync(role, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.PermissionIds.Count > 0)
        {
            var validPermissionIds = await _context.Permissions
                .Where(p => request.PermissionIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (var permissionId in validPermissionIds)
            {
                await _context.RolePermissions.AddAsync(
                    new RolePermission { RoleId = role.Id, PermissionId = permissionId }, cancellationToken);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        await _auditService.LogAsync(Domain.Enums.AuditAction.Create, "Roles", nameof(Role), role.Id.ToString(),
            ct: cancellationToken);

        return role.Id;
    }
}
