using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Visitors;

public class GateDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateGateCommand(int SocietyId, string Name, string Code, string? Location) : IRequest<int>;

public class CreateGateCommandValidator : AbstractValidator<CreateGateCommand>
{
    public CreateGateCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
    }
}

public record UpdateGateCommand(int Id, string Name, string Code, string? Location, bool IsActive) : IRequest<Unit>;

public class UpdateGateCommandValidator : AbstractValidator<UpdateGateCommand>
{
    public UpdateGateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
    }
}

public record DeleteGateCommand(int Id) : IRequest<Unit>;

public class GateCommandHandlers :
    IRequestHandler<CreateGateCommand, int>,
    IRequestHandler<UpdateGateCommand, Unit>,
    IRequestHandler<DeleteGateCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public GateCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateGateCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }
        if (await _context.Gates.AnyAsync(g => g.SocietyId == request.SocietyId && g.Code == request.Code && !g.IsDeleted, ct))
        {
            throw new ConflictAppException("A gate with this code already exists.");
        }

        var gate = new Gate { SocietyId = request.SocietyId, Name = request.Name, Code = request.Code, Location = request.Location };
        await _context.Gates.AddAsync(gate, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Visitors", nameof(Gate), gate.Id.ToString(), ct: ct);
        return gate.Id;
    }

    public async Task<Unit> Handle(UpdateGateCommand request, CancellationToken ct)
    {
        var gate = await _context.Gates.FirstOrDefaultAsync(g => g.Id == request.Id && !g.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Gate), request.Id);

        gate.Name = request.Name;
        gate.Code = request.Code;
        gate.Location = request.Location;
        gate.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Visitors", nameof(Gate), gate.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteGateCommand request, CancellationToken ct)
    {
        var gate = await _context.Gates.FirstOrDefaultAsync(g => g.Id == request.Id && !g.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Gate), request.Id);

        if (await _context.VisitorVisits.AnyAsync(v => v.GateId == gate.Id && !v.IsDeleted, ct))
        {
            throw new ConflictAppException("Cannot delete a gate that has visitor visits on file.");
        }

        gate.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Visitors", nameof(Gate), gate.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetGatesQuery(int SocietyId, bool? IsActive) : IRequest<List<GateDto>>;

public class GateQueryHandlers : IRequestHandler<GetGatesQuery, List<GateDto>>
{
    private readonly IApplicationDbContext _context;

    public GateQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<GateDto>> Handle(GetGatesQuery request, CancellationToken ct)
    {
        var query = _context.Gates.Where(g => g.SocietyId == request.SocietyId);
        if (request.IsActive.HasValue) query = query.Where(g => g.IsActive == request.IsActive);

        return await query
            .OrderBy(g => g.Name)
            .Select(g => new GateDto { Id = g.Id, SocietyId = g.SocietyId, Name = g.Name, Code = g.Code, Location = g.Location, IsActive = g.IsActive })
            .ToListAsync(ct);
    }
}
