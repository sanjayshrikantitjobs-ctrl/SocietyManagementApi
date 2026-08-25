using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Users.Commands.UpdateUser;

/// <summary><see cref="SocietyId"/> is only ever applied when the caller is
/// Super Admin (same "never trust client input for the tenant boundary"
/// rule as CreateUserCommand) — a scoped Admin's edit ignores whatever
/// value is sent, leaving the target's existing SocietyId untouched.</summary>
public record UpdateUserCommand(
    int Id,
    string FirstName,
    string LastName,
    string MobileNumber,
    int RoleId,
    bool IsActive,
    int? SocietyId = null) : IRequest<Unit>;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MobileNumber).NotEmpty().Must(m => m.IsValidIndianMobile());
        RuleFor(x => x.RoleId).GreaterThan(0);
    }
}

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateUserCommandHandler(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        var duplicateMobile = await _context.Users.AnyAsync(
            u => u.MobileNumber == request.MobileNumber && u.Id != request.Id && !u.IsDeleted, cancellationToken);
        if (duplicateMobile)
        {
            throw new ConflictAppException("A user with this mobile number already exists.");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.MobileNumber = request.MobileNumber;
        user.RoleId = request.RoleId;
        user.IsActive = request.IsActive;

        if (_currentUserService.SocietyId is null)
        {
            if (role.Name == Shared.Constants.Roles.SuperAdmin)
            {
                user.SocietyId = null;
            }
            else if (request.SocietyId.HasValue)
            {
                if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, cancellationToken))
                {
                    throw new NotFoundException(nameof(Society), request.SocietyId.Value);
                }
                user.SocietyId = request.SocietyId;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Users", nameof(User), user.Id.ToString(),
            ct: cancellationToken);

        return Unit.Value;
    }
}
