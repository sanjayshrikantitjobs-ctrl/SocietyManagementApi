using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.ParkingSlots;

public class ParkingSlotDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string SlotNumber { get; set; } = default!;
    public ParkingType Type { get; set; }
    public ParkingStatus Status { get; set; }
    public int? AllocatedFlatId { get; set; }
    public string? AllocatedFlatNumber { get; set; }
}

public record CreateParkingSlotCommand(int SocietyId, string SlotNumber, ParkingType Type) : IRequest<int>;
public class CreateParkingSlotCommandValidator : AbstractValidator<CreateParkingSlotCommand>
{
    public CreateParkingSlotCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.SlotNumber).NotEmpty().MaximumLength(20);
    }
}

public record AllocateParkingSlotCommand(int Id, int? FlatId) : IRequest<Unit>;

public record DeleteParkingSlotCommand(int Id) : IRequest<Unit>;

public class ParkingSlotCommandHandlers :
    IRequestHandler<CreateParkingSlotCommand, int>,
    IRequestHandler<AllocateParkingSlotCommand, Unit>,
    IRequestHandler<DeleteParkingSlotCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public ParkingSlotCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateParkingSlotCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }
        var slot = new ParkingSlot { SocietyId = request.SocietyId, SlotNumber = request.SlotNumber, Type = request.Type, Status = ParkingStatus.Vacant };
        await _context.ParkingSlots.AddAsync(slot, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Society", nameof(ParkingSlot), slot.Id.ToString(), ct: ct);
        return slot.Id;
    }

    public async Task<Unit> Handle(AllocateParkingSlotCommand request, CancellationToken ct)
    {
        var slot = await _context.ParkingSlots.FirstOrDefaultAsync(p => p.Id == request.Id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ParkingSlot), request.Id);

        if (request.FlatId.HasValue &&
            !await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId.Value);
        }

        slot.AllocatedFlatId = request.FlatId;
        slot.Status = request.FlatId.HasValue ? ParkingStatus.Allocated : ParkingStatus.Vacant;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Society", nameof(ParkingSlot), slot.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteParkingSlotCommand request, CancellationToken ct)
    {
        var slot = await _context.ParkingSlots.FirstOrDefaultAsync(p => p.Id == request.Id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ParkingSlot), request.Id);
        slot.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Society", nameof(ParkingSlot), slot.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

public record GetParkingSlotsQuery(int SocietyId, ParkingStatus? Status) : IRequest<List<ParkingSlotDto>>;

public class ParkingSlotQueryHandlers : IRequestHandler<GetParkingSlotsQuery, List<ParkingSlotDto>>
{
    private readonly IApplicationDbContext _context;

    public ParkingSlotQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<ParkingSlotDto>> Handle(GetParkingSlotsQuery request, CancellationToken ct)
    {
        var query = _context.ParkingSlots.Where(p => p.SocietyId == request.SocietyId && !p.IsDeleted);
        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status);
        }

        return await query.OrderBy(p => p.SlotNumber)
            .Select(p => new ParkingSlotDto
            {
                Id = p.Id, SocietyId = p.SocietyId, SlotNumber = p.SlotNumber, Type = p.Type, Status = p.Status,
                AllocatedFlatId = p.AllocatedFlatId,
                AllocatedFlatNumber = p.AllocatedFlat != null ? p.AllocatedFlat.FlatNumber : null
            })
            .ToListAsync(ct);
    }
}
