using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Buildings;

public class BuildingDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int WingCount { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateBuildingCommand(int SocietyId, string Name, string? Description) : IRequest<int>;

public class CreateBuildingCommandValidator : AbstractValidator<CreateBuildingCommand>
{
    public CreateBuildingCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public record UpdateBuildingCommand(int Id, string Name, string? Description) : IRequest<Unit>;

public class UpdateBuildingCommandValidator : AbstractValidator<UpdateBuildingCommand>
{
    public UpdateBuildingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public record DeleteBuildingCommand(int Id) : IRequest<Unit>;

public class BuildingCommandHandlers :
    IRequestHandler<CreateBuildingCommand, int>,
    IRequestHandler<UpdateBuildingCommand, Unit>,
    IRequestHandler<DeleteBuildingCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public BuildingCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateBuildingCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var building = new Building { SocietyId = request.SocietyId, Name = request.Name, Description = request.Description };
        await _context.Buildings.AddAsync(building, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Create, "Society", nameof(Building), building.Id.ToString(), ct: ct);
        return building.Id;
    }

    public async Task<Unit> Handle(UpdateBuildingCommand request, CancellationToken ct)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Building), request.Id);
        building.Name = request.Name;
        building.Description = request.Description;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Society", nameof(Building), building.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteBuildingCommand request, CancellationToken ct)
    {
        var building = await _context.Buildings.Include(b => b.Wings)
            .FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Building), request.Id);
        if (building.Wings.Any(w => !w.IsDeleted))
        {
            throw new ConflictAppException("Cannot delete a building that still has wings. Remove wings first.");
        }
        building.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Delete, "Society", nameof(Building), building.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetBuildingsQuery(int SocietyId) : IRequest<List<BuildingDto>>;

public record GetBuildingByIdQuery(int Id) : IRequest<BuildingDto>;

public class BuildingQueryHandlers :
    IRequestHandler<GetBuildingsQuery, List<BuildingDto>>,
    IRequestHandler<GetBuildingByIdQuery, BuildingDto>
{
    private readonly IApplicationDbContext _context;

    public BuildingQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<BuildingDto>> Handle(GetBuildingsQuery request, CancellationToken ct) =>
        await _context.Buildings.Where(b => b.SocietyId == request.SocietyId && !b.IsDeleted)
            .Select(b => new BuildingDto
            {
                Id = b.Id, SocietyId = b.SocietyId, Name = b.Name, Description = b.Description,
                WingCount = b.Wings.Count(w => !w.IsDeleted)
            })
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

    public async Task<BuildingDto> Handle(GetBuildingByIdQuery request, CancellationToken ct) =>
        await _context.Buildings.Where(b => b.Id == request.Id && !b.IsDeleted)
            .Select(b => new BuildingDto
            {
                Id = b.Id, SocietyId = b.SocietyId, Name = b.Name, Description = b.Description,
                WingCount = b.Wings.Count(w => !w.IsDeleted)
            })
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Building), request.Id);
}
