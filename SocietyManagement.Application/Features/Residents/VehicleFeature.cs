using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Residents;

public class VehicleDto
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public string? MemberName { get; set; }
    public VehicleType VehicleType { get; set; }
    public string RegistrationNumber { get; set; } = default!;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public int? ParkingSlotId { get; set; }
    public string? ParkingSlotNumber { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateVehicleCommand(
    int MemberId, VehicleType VehicleType, string RegistrationNumber,
    string? Make, string? Model, string? Color, int? ParkingSlotId) : IRequest<int>;

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.MemberId).GreaterThan(0);
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(20);
    }
}

public record UpdateVehicleCommand(
    int Id, VehicleType VehicleType, string RegistrationNumber,
    string? Make, string? Model, string? Color, int? ParkingSlotId) : IRequest<Unit>;

public class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(20);
    }
}

public record DeleteVehicleCommand(int Id) : IRequest<Unit>;

public class VehicleCommandHandlers :
    IRequestHandler<CreateVehicleCommand, int>,
    IRequestHandler<UpdateVehicleCommand, Unit>,
    IRequestHandler<DeleteVehicleCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public VehicleCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateVehicleCommand request, CancellationToken ct)
    {
        if (!await _context.Members.AnyAsync(m => m.Id == request.MemberId && !m.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Member), request.MemberId);
        }
        if (await _context.Vehicles.AnyAsync(v => v.RegistrationNumber == request.RegistrationNumber && !v.IsDeleted, ct))
        {
            throw new ConflictAppException("A vehicle with this registration number is already on file.");
        }

        var vehicle = new Vehicle
        {
            MemberId = request.MemberId, VehicleType = request.VehicleType, RegistrationNumber = request.RegistrationNumber,
            Make = request.Make, Model = request.Model, Color = request.Color, ParkingSlotId = request.ParkingSlotId
        };
        await _context.Vehicles.AddAsync(vehicle, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Residents", nameof(Vehicle), vehicle.Id.ToString(), ct: ct);
        return vehicle.Id;
    }

    public async Task<Unit> Handle(UpdateVehicleCommand request, CancellationToken ct)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Vehicle), request.Id);

        vehicle.VehicleType = request.VehicleType;
        vehicle.RegistrationNumber = request.RegistrationNumber;
        vehicle.Make = request.Make;
        vehicle.Model = request.Model;
        vehicle.Color = request.Color;
        vehicle.ParkingSlotId = request.ParkingSlotId;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(Vehicle), vehicle.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteVehicleCommand request, CancellationToken ct)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Vehicle), request.Id);

        vehicle.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Residents", nameof(Vehicle), vehicle.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetVehiclesQuery(int? MemberId, int? SocietyId) : IRequest<List<VehicleDto>>;

public class VehicleQueryHandlers : IRequestHandler<GetVehiclesQuery, List<VehicleDto>>
{
    private readonly IApplicationDbContext _context;

    public VehicleQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<VehicleDto>> Handle(GetVehiclesQuery request, CancellationToken ct)
    {
        var query = _context.Vehicles.Where(v => !v.IsDeleted);

        if (request.MemberId.HasValue) query = query.Where(v => v.MemberId == request.MemberId);
        if (request.SocietyId.HasValue) query = query.Where(v => v.Member.SocietyId == request.SocietyId);

        return await query
            .OrderBy(v => v.RegistrationNumber)
            .Select(v => new VehicleDto
            {
                Id = v.Id, MemberId = v.MemberId, MemberName = v.Member.FirstName + " " + v.Member.LastName,
                VehicleType = v.VehicleType, RegistrationNumber = v.RegistrationNumber, Make = v.Make, Model = v.Model,
                Color = v.Color, ParkingSlotId = v.ParkingSlotId,
                ParkingSlotNumber = v.ParkingSlot != null ? v.ParkingSlot.SlotNumber : null
            })
            .ToListAsync(ct);
    }
}
