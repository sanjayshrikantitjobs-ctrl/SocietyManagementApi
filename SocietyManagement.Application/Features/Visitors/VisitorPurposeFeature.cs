using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Visitors;

public class VisitorPurposeDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public bool RequiresApproval { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreatePurposeCommand(int SocietyId, string Name, bool RequiresApproval, int DisplayOrder) : IRequest<int>;

public class CreatePurposeCommandValidator : AbstractValidator<CreatePurposeCommand>
{
    public CreatePurposeCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public record UpdatePurposeCommand(int Id, string Name, bool RequiresApproval, bool IsActive, int DisplayOrder) : IRequest<Unit>;

public class UpdatePurposeCommandValidator : AbstractValidator<UpdatePurposeCommand>
{
    public UpdatePurposeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public record DeletePurposeCommand(int Id) : IRequest<Unit>;

public class VisitorPurposeCommandHandlers :
    IRequestHandler<CreatePurposeCommand, int>,
    IRequestHandler<UpdatePurposeCommand, Unit>,
    IRequestHandler<DeletePurposeCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public VisitorPurposeCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreatePurposeCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }
        if (await _context.VisitorPurposes.AnyAsync(p => p.SocietyId == request.SocietyId && p.Name == request.Name && !p.IsDeleted, ct))
        {
            throw new ConflictAppException("A purpose with this name already exists.");
        }

        var purpose = new VisitorPurpose
        {
            SocietyId = request.SocietyId, Name = request.Name, RequiresApproval = request.RequiresApproval,
            DisplayOrder = request.DisplayOrder
        };
        await _context.VisitorPurposes.AddAsync(purpose, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Visitors", nameof(VisitorPurpose), purpose.Id.ToString(), ct: ct);
        return purpose.Id;
    }

    public async Task<Unit> Handle(UpdatePurposeCommand request, CancellationToken ct)
    {
        var purpose = await _context.VisitorPurposes.FirstOrDefaultAsync(p => p.Id == request.Id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitorPurpose), request.Id);

        purpose.Name = request.Name;
        purpose.RequiresApproval = request.RequiresApproval;
        purpose.IsActive = request.IsActive;
        purpose.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Visitors", nameof(VisitorPurpose), purpose.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeletePurposeCommand request, CancellationToken ct)
    {
        var purpose = await _context.VisitorPurposes.FirstOrDefaultAsync(p => p.Id == request.Id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitorPurpose), request.Id);

        if (await _context.VisitorVisits.AnyAsync(v => v.PurposeId == purpose.Id && !v.IsDeleted, ct))
        {
            throw new ConflictAppException("Cannot delete a purpose that has visitor visits on file. Deactivate it instead.");
        }

        purpose.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Visitors", nameof(VisitorPurpose), purpose.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetPurposesQuery(int SocietyId, bool? IsActive) : IRequest<List<VisitorPurposeDto>>;

public class VisitorPurposeQueryHandlers : IRequestHandler<GetPurposesQuery, List<VisitorPurposeDto>>
{
    private readonly IApplicationDbContext _context;

    public VisitorPurposeQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<VisitorPurposeDto>> Handle(GetPurposesQuery request, CancellationToken ct)
    {
        var query = _context.VisitorPurposes.Where(p => p.SocietyId == request.SocietyId);
        if (request.IsActive.HasValue) query = query.Where(p => p.IsActive == request.IsActive);

        return await query
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .Select(p => new VisitorPurposeDto
            {
                Id = p.Id, SocietyId = p.SocietyId, Name = p.Name, RequiresApproval = p.RequiresApproval,
                IsActive = p.IsActive, DisplayOrder = p.DisplayOrder
            })
            .ToListAsync(ct);
    }
}
