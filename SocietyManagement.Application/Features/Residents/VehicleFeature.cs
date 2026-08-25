using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Residents;

public class VehicleDto
{
    public int Id { get; set; }
    public int? MemberId { get; set; }
    public string? MemberName { get; set; }
    public int? FlatId { get; set; }
    public string? FlatNumber { get; set; }
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
    int? MemberId, int? FlatId, VehicleType VehicleType, string RegistrationNumber,
    string? Make, string? Model, string? Color, int? ParkingSlotId) : IRequest<int>;

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x).Must(x => x.MemberId.HasValue || x.FlatId.HasValue)
            .WithMessage("A vehicle must be assigned to either a member or a flat.");
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(20);
    }
}

public record UpdateVehicleCommand(
    int Id, int? MemberId, int? FlatId, VehicleType VehicleType, string RegistrationNumber,
    string? Make, string? Model, string? Color, int? ParkingSlotId) : IRequest<Unit>;

public class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x).Must(x => x.MemberId.HasValue || x.FlatId.HasValue)
            .WithMessage("A vehicle must be assigned to either a member or a flat.");
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
        if (request.MemberId.HasValue && !await _context.Members.AnyAsync(m => m.Id == request.MemberId && !m.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Member), request.MemberId.Value);
        }
        if (request.FlatId.HasValue && !await _context.Flats.AnyAsync(fl => fl.Id == request.FlatId && !fl.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId.Value);
        }
        if (await _context.Vehicles.AnyAsync(v => v.RegistrationNumber == request.RegistrationNumber && !v.IsDeleted, ct))
        {
            throw new ConflictAppException("A vehicle with this registration number is already on file.");
        }

        var vehicle = new Vehicle
        {
            MemberId = request.MemberId, FlatId = request.FlatId, VehicleType = request.VehicleType,
            RegistrationNumber = request.RegistrationNumber,
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

        if (request.MemberId.HasValue && !await _context.Members.AnyAsync(m => m.Id == request.MemberId && !m.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Member), request.MemberId.Value);
        }
        if (request.FlatId.HasValue && !await _context.Flats.AnyAsync(fl => fl.Id == request.FlatId && !fl.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId.Value);
        }

        vehicle.MemberId = request.MemberId;
        vehicle.FlatId = request.FlatId;
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
public record GetVehiclesQuery(
    int? MemberId, int? FlatId, int? SocietyId, string? Search, string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<VehicleDto>>;

public class VehicleQueryHandlers : IRequestHandler<GetVehiclesQuery, PaginatedResult<VehicleDto>>
{
    private readonly IApplicationDbContext _context;

    public VehicleQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedResult<VehicleDto>> Handle(GetVehiclesQuery request, CancellationToken ct)
    {
        var query = _context.Vehicles.Where(v => !v.IsDeleted);

        if (request.MemberId.HasValue) query = query.Where(v => v.MemberId == request.MemberId);
        if (request.FlatId.HasValue) query = query.Where(v => v.FlatId == request.FlatId);
        if (request.SocietyId.HasValue)
        {
            query = query.Where(v =>
                (v.Member != null && v.Member.SocietyId == request.SocietyId) ||
                (v.Flat != null && v.Flat.Floor.Wing.Building.SocietyId == request.SocietyId));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(v => v.RegistrationNumber.ToLower().Contains(term)
                || (v.Member != null && (v.Member.FirstName.ToLower().Contains(term) || v.Member.LastName.ToLower().Contains(term)))
                || (v.Flat != null && v.Flat.FlatNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("membername", false) => query.OrderBy(v => v.Member == null ? null : v.Member.FirstName).ThenBy(v => v.Member == null ? null : v.Member.LastName),
            ("membername", true) => query.OrderByDescending(v => v.Member == null ? null : v.Member.FirstName).ThenByDescending(v => v.Member == null ? null : v.Member.LastName),
            ("vehicletype", false) => query.OrderBy(v => v.VehicleType),
            ("vehicletype", true) => query.OrderByDescending(v => v.VehicleType),
            ("makemodel", false) => query.OrderBy(v => v.Make).ThenBy(v => v.Model),
            ("makemodel", true) => query.OrderByDescending(v => v.Make).ThenByDescending(v => v.Model),
            ("parking", false) => query.OrderBy(v => v.ParkingSlot == null ? null : v.ParkingSlot.SlotNumber),
            ("parking", true) => query.OrderByDescending(v => v.ParkingSlot == null ? null : v.ParkingSlot.SlotNumber),
            ("registrationnumber", true) => query.OrderByDescending(v => v.RegistrationNumber),
            _ => query.OrderBy(v => v.RegistrationNumber)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VehicleDto
            {
                Id = v.Id, MemberId = v.MemberId,
                MemberName = v.Member != null ? v.Member.FirstName + " " + v.Member.LastName : null,
                FlatId = v.FlatId, FlatNumber = v.Flat != null ? v.Flat.FlatNumber : null,
                VehicleType = v.VehicleType, RegistrationNumber = v.RegistrationNumber, Make = v.Make, Model = v.Model,
                Color = v.Color, ParkingSlotId = v.ParkingSlotId,
                ParkingSlotNumber = v.ParkingSlot != null ? v.ParkingSlot.SlotNumber : null
            })
            .ToListAsync(ct);

        return new PaginatedResult<VehicleDto>(items, totalCount, pageNumber, pageSize);
    }
}
