using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Users.Commands.UpdateUser;

public record UpdateUserCommand(
    int Id,
    string FirstName,
    string LastName,
    string MobileNumber,
    int RoleId,
    bool IsActive) : IRequest<Unit>;

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

    public UpdateUserCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        if (!await _context.Roles.AnyAsync(r => r.Id == request.RoleId && !r.IsDeleted, cancellationToken))
        {
            throw new NotFoundException(nameof(Role), request.RoleId);
        }

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

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Users", nameof(User), user.Id.ToString(),
            ct: cancellationToken);

        return Unit.Value;
    }
}
