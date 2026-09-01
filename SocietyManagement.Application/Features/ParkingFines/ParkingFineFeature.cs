using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Helpers;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.ParkingFines;

// ---- DTOs ----------------------------------------------------------------------

public class ParkingFineDto
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string RegistrationNumber { get; set; } = default!;
    public string? FlatNumber { get; set; }
    public string? ParkingSlotNumber { get; set; }
    public ParkingFineReason Reason { get; set; }
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime FineDate { get; set; }
    public string IssuedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

// ---- Commands --------------------------------------------------------------------

/// <summary>PhotoBytes is optional throughout — a broken/unavailable camera
/// shouldn't block recording a fine (see ParkingFine.PhotoUrl doc comment).</summary>
public record CreateParkingFineCommand(
    int SocietyId, int VehicleId, int? ParkingSlotId, ParkingFineReason Reason,
    string? Notes, decimal Amount, DateTime FineDate, byte[]? PhotoBytes) : IRequest<int>;

public class CreateParkingFineCommandValidator : AbstractValidator<CreateParkingFineCommand>
{
    public CreateParkingFineCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.ParkingSlotId).NotNull()
            .When(x => x.Reason == ParkingFineReason.WrongAllottedSlot)
            .WithMessage("A parking slot is required when the reason is 'parked in someone else's allotted slot'.");
    }
}

public record DeleteParkingFineCommand(int Id) : IRequest<Unit>;

// ---- Queries -------------------------------------------------------------------

/// <summary>All-society scope for Watchman too (unlike GetScanHistoryQuery's
/// per-actor scoping) — matches Visitors.View's breadth, useful for handing
/// off between shifts. Watchman just never sees Delete.</summary>
public record GetParkingFinesQuery(
    int SocietyId, int? VehicleId, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<ParkingFineDto>>;

// ---- Handlers --------------------------------------------------------------------

public class ParkingFineHandlers :
    IRequestHandler<CreateParkingFineCommand, int>,
    IRequestHandler<DeleteParkingFineCommand, Unit>,
    IRequestHandler<GetParkingFinesQuery, PaginatedResult<ParkingFineDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditService _auditService;

    public ParkingFineHandlers(
        IApplicationDbContext context, ICurrentUserService currentUserService,
        IFileStorageService fileStorage, IAuditService auditService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateParkingFineCommand request, CancellationToken ct)
    {
        var vehicle = await _context.Vehicles
            .Where(v => v.Id == request.VehicleId && !v.IsDeleted)
            .Where(v =>
                (v.Member != null && v.Member.SocietyId == request.SocietyId) ||
                (v.Flat != null && v.Flat.Floor.Wing.Building.SocietyId == request.SocietyId))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);

        if (request.ParkingSlotId.HasValue)
        {
            var slotBelongsToSociety = await _context.ParkingSlots
                .AnyAsync(p => p.Id == request.ParkingSlotId && p.SocietyId == request.SocietyId && !p.IsDeleted, ct);
            if (!slotBelongsToSociety)
            {
                throw new NotFoundException(nameof(ParkingSlot), request.ParkingSlotId.Value);
            }
        }

        string? photoUrl = null;
        if (request.PhotoBytes is { Length: > 0 })
        {
            var normalized = VehicleNumberNormalizer.Normalize(vehicle.RegistrationNumber);
            photoUrl = await _fileStorage.SaveAsync(request.PhotoBytes, $"{normalized}_{Guid.NewGuid():N}.jpg", "parking-fines", ct);
        }

        var fine = new ParkingFine
        {
            SocietyId = request.SocietyId,
            VehicleId = request.VehicleId,
            ParkingSlotId = request.ParkingSlotId,
            Reason = request.Reason,
            Notes = request.Notes,
            Amount = request.Amount,
            PhotoUrl = photoUrl,
            FineDate = request.FineDate,
            IssuedByUserId = _currentUserService.UserId!.Value
        };
        await _context.ParkingFines.AddAsync(fine, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "ParkingFines", nameof(ParkingFine), fine.Id.ToString(), ct: ct);

        return fine.Id;
    }

    public async Task<Unit> Handle(DeleteParkingFineCommand request, CancellationToken ct)
    {
        var fine = await _context.ParkingFines.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ParkingFine), request.Id);

        fine.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "ParkingFines", nameof(ParkingFine), fine.Id.ToString(), ct: ct);

        return Unit.Value;
    }

    public async Task<PaginatedResult<ParkingFineDto>> Handle(GetParkingFinesQuery request, CancellationToken ct)
    {
        var query = _context.ParkingFines.Where(f => f.SocietyId == request.SocietyId);

        if (request.VehicleId.HasValue)
        {
            query = query.Where(f => f.VehicleId == request.VehicleId);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(f => f.Vehicle.RegistrationNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderByDescending(f => f.FineDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new ParkingFineDto
            {
                Id = f.Id,
                VehicleId = f.VehicleId,
                RegistrationNumber = f.Vehicle.RegistrationNumber,
                FlatNumber = f.Vehicle.Flat != null ? f.Vehicle.Flat.FlatNumber : null,
                ParkingSlotNumber = f.ParkingSlot != null ? f.ParkingSlot.SlotNumber : null,
                Reason = f.Reason,
                Notes = f.Notes,
                Amount = f.Amount,
                PhotoUrl = f.PhotoUrl,
                FineDate = f.FineDate,
                IssuedByName = f.IssuedByUser.FirstName + " " + f.IssuedByUser.LastName,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync(ct);

        return new PaginatedResult<ParkingFineDto>(items, totalCount, pageNumber, pageSize);
    }
}
