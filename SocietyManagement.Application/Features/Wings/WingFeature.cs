using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Wings;

public class WingDto
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public string Name { get; set; } = default!;
    public int FloorCount { get; set; }
}

public record CreateWingCommand(int BuildingId, string Name) : IRequest<int>;
public class CreateWingCommandValidator : AbstractValidator<CreateWingCommand>
{
    public CreateWingCommandValidator()
    {
        RuleFor(x => x.BuildingId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public record UpdateWingCommand(int Id, string Name) : IRequest<Unit>;
public class UpdateWingCommandValidator : AbstractValidator<UpdateWingCommand>
{
    public UpdateWingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public record DeleteWingCommand(int Id) : IRequest<Unit>;

public class WingCommandHandlers :
    IRequestHandler<CreateWingCommand, int>,
    IRequestHandler<UpdateWingCommand, Unit>,
    IRequestHandler<DeleteWingCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public WingCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateWingCommand request, CancellationToken ct)
    {
        if (!await _context.Buildings.AnyAsync(b => b.Id == request.BuildingId && !b.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Building), request.BuildingId);
        }
        var wing = new Wing { BuildingId = request.BuildingId, Name = request.Name };
        await _context.Wings.AddAsync(wing, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Create, "Society", nameof(Wing), wing.Id.ToString(), ct: ct);
        return wing.Id;
    }

    public async Task<Unit> Handle(UpdateWingCommand request, CancellationToken ct)
    {
        var wing = await _context.Wings.FirstOrDefaultAsync(w => w.Id == request.Id && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Wing), request.Id);
        wing.Name = request.Name;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Society", nameof(Wing), wing.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteWingCommand request, CancellationToken ct)
    {
        var wing = await _context.Wings.Include(w => w.Floors)
            .FirstOrDefaultAsync(w => w.Id == request.Id && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Wing), request.Id);
        if (wing.Floors.Any(f => !f.IsDeleted))
        {
            throw new ConflictAppException("Cannot delete a wing that still has floors. Remove floors first.");
        }
        wing.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Delete, "Society", nameof(Wing), wing.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

public record GetWingsQuery(int BuildingId) : IRequest<List<WingDto>>;
public record GetWingByIdQuery(int Id) : IRequest<WingDto>;

public class WingQueryHandlers :
    IRequestHandler<GetWingsQuery, List<WingDto>>,
    IRequestHandler<GetWingByIdQuery, WingDto>
{
    private readonly IApplicationDbContext _context;

    public WingQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<WingDto>> Handle(GetWingsQuery request, CancellationToken ct) =>
        await _context.Wings.Where(w => w.BuildingId == request.BuildingId && !w.IsDeleted)
            .Select(w => new WingDto { Id = w.Id, BuildingId = w.BuildingId, Name = w.Name, FloorCount = w.Floors.Count(f => !f.IsDeleted) })
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

    public async Task<WingDto> Handle(GetWingByIdQuery request, CancellationToken ct) =>
        await _context.Wings.Where(w => w.Id == request.Id && !w.IsDeleted)
            .Select(w => new WingDto { Id = w.Id, BuildingId = w.BuildingId, Name = w.Name, FloorCount = w.Floors.Count(f => !f.IsDeleted) })
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Wing), request.Id);
}
