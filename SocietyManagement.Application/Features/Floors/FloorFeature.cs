using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Floors;

public class FloorDto
{
    public int Id { get; set; }
    public int WingId { get; set; }
    public int FloorNumber { get; set; }
    public string? Name { get; set; }
    public int FlatCount { get; set; }
}

public record CreateFloorCommand(int WingId, int FloorNumber, string? Name) : IRequest<int>;
public class CreateFloorCommandValidator : AbstractValidator<CreateFloorCommand>
{
    public CreateFloorCommandValidator()
    {
        RuleFor(x => x.WingId).GreaterThan(0);
        RuleFor(x => x.FloorNumber).GreaterThanOrEqualTo(0);
    }
}

public record UpdateFloorCommand(int Id, int FloorNumber, string? Name) : IRequest<Unit>;
public class UpdateFloorCommandValidator : AbstractValidator<UpdateFloorCommand>
{
    public UpdateFloorCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FloorNumber).GreaterThanOrEqualTo(0);
    }
}

public record DeleteFloorCommand(int Id) : IRequest<Unit>;

public class FloorCommandHandlers :
    IRequestHandler<CreateFloorCommand, int>,
    IRequestHandler<UpdateFloorCommand, Unit>,
    IRequestHandler<DeleteFloorCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FloorCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateFloorCommand request, CancellationToken ct)
    {
        if (!await _context.Wings.AnyAsync(w => w.Id == request.WingId && !w.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Wing), request.WingId);
        }
        var floor = new Floor { WingId = request.WingId, FloorNumber = request.FloorNumber, Name = request.Name };
        await _context.Floors.AddAsync(floor, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Create, "Society", nameof(Floor), floor.Id.ToString(), ct: ct);
        return floor.Id;
    }

    public async Task<Unit> Handle(UpdateFloorCommand request, CancellationToken ct)
    {
        var floor = await _context.Floors.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Floor), request.Id);
        floor.FloorNumber = request.FloorNumber;
        floor.Name = request.Name;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Society", nameof(Floor), floor.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteFloorCommand request, CancellationToken ct)
    {
        var floor = await _context.Floors.Include(f => f.Flats)
            .FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Floor), request.Id);
        if (floor.Flats.Any(fl => !fl.IsDeleted))
        {
            throw new ConflictAppException("Cannot delete a floor that still has flats. Remove flats first.");
        }
        floor.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Delete, "Society", nameof(Floor), floor.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

public record GetFloorsQuery(int WingId) : IRequest<List<FloorDto>>;
public record GetFloorByIdQuery(int Id) : IRequest<FloorDto>;

public class FloorQueryHandlers :
    IRequestHandler<GetFloorsQuery, List<FloorDto>>,
    IRequestHandler<GetFloorByIdQuery, FloorDto>
{
    private readonly IApplicationDbContext _context;

    public FloorQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<FloorDto>> Handle(GetFloorsQuery request, CancellationToken ct) =>
        await _context.Floors.Where(f => f.WingId == request.WingId && !f.IsDeleted)
            .Select(f => new FloorDto { Id = f.Id, WingId = f.WingId, FloorNumber = f.FloorNumber, Name = f.Name, FlatCount = f.Flats.Count(fl => !fl.IsDeleted) })
            .OrderBy(f => f.FloorNumber)
            .ToListAsync(ct);

    public async Task<FloorDto> Handle(GetFloorByIdQuery request, CancellationToken ct) =>
        await _context.Floors.Where(f => f.Id == request.Id && !f.IsDeleted)
            .Select(f => new FloorDto { Id = f.Id, WingId = f.WingId, FloorNumber = f.FloorNumber, Name = f.Name, FlatCount = f.Flats.Count(fl => !fl.IsDeleted) })
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Floor), request.Id);
}
