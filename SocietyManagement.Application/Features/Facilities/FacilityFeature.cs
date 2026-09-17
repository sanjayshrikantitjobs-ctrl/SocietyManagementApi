using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Facilities;

public class FacilityDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public FacilityType Type { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Location { get; set; }
    public int Capacity { get; set; }
    public FacilityPricingType PricingType { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal CleaningCharge { get; set; }
    public decimal AdditionalCharge { get; set; }
    public bool RequiresApproval { get; set; }
    public int AdvanceBookingDaysLimit { get; set; }
    public int CancellationHoursBeforeStart { get; set; }
    public bool IsActive { get; set; }
}

public class FacilityBlackoutDateDto
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public DateTime BlackoutDate { get; set; }
    public string? Reason { get; set; }
}

/// <summary>One booked/blocked window on a facility's calendar for a given
/// day — backs the Available/Booked/Pending/Blocked availability view.</summary>
public class FacilitySlotDto
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public FacilityBookingStatus Status { get; set; }
}

// ==================== Commands ====================

public record CreateFacilityCommand(
    int SocietyId, string Name, FacilityType Type, string? Description, string? ImageUrl, string? Location,
    int Capacity, FacilityPricingType PricingType, decimal PricePerUnit, decimal SecurityDeposit, decimal CleaningCharge,
    decimal AdditionalCharge, bool RequiresApproval, int AdvanceBookingDaysLimit, int CancellationHoursBeforeStart) : IRequest<int>;

public class CreateFacilityCommandValidator : AbstractValidator<CreateFacilityCommand>
{
    public CreateFacilityCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PricePerUnit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CleaningCharge).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AdditionalCharge).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AdvanceBookingDaysLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CancellationHoursBeforeStart).GreaterThanOrEqualTo(0);
    }
}

public record UpdateFacilityCommand(
    int Id, string Name, FacilityType Type, string? Description, string? ImageUrl, string? Location,
    int Capacity, FacilityPricingType PricingType, decimal PricePerUnit, decimal SecurityDeposit, decimal CleaningCharge,
    decimal AdditionalCharge, bool RequiresApproval, int AdvanceBookingDaysLimit, int CancellationHoursBeforeStart, bool IsActive) : IRequest;

public class UpdateFacilityCommandValidator : AbstractValidator<UpdateFacilityCommand>
{
    public UpdateFacilityCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0);
    }
}

public record DeleteFacilityCommand(int Id) : IRequest;

public record AddFacilityBlackoutDateCommand(int FacilityId, DateTime BlackoutDate, string? Reason) : IRequest<int>;

public record RemoveFacilityBlackoutDateCommand(int Id) : IRequest;

// ==================== Queries ====================

public record GetFacilitiesQuery(int SocietyId, bool ActiveOnly) : IRequest<List<FacilityDto>>;

public record GetFacilityByIdQuery(int Id) : IRequest<FacilityDto>;

public record GetFacilityBlackoutDatesQuery(int FacilityId) : IRequest<List<FacilityBlackoutDateDto>>;

/// <summary>Every non-Rejected/Cancelled booking on this facility for the
/// given date, so the UI can render Available/Booked/Pending time slots.</summary>
public record GetFacilityAvailabilityQuery(int FacilityId, DateTime Date) : IRequest<List<FacilitySlotDto>>;

// ==================== Handlers ====================

public class FacilityCommandHandlers :
    IRequestHandler<CreateFacilityCommand, int>,
    IRequestHandler<UpdateFacilityCommand>,
    IRequestHandler<DeleteFacilityCommand>,
    IRequestHandler<AddFacilityBlackoutDateCommand, int>,
    IRequestHandler<RemoveFacilityBlackoutDateCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FacilityCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateFacilityCommand request, CancellationToken ct)
    {
        var facility = new Facility
        {
            SocietyId = request.SocietyId, Name = request.Name, Type = request.Type, Description = request.Description,
            ImageUrl = request.ImageUrl, Location = request.Location, Capacity = request.Capacity, PricingType = request.PricingType,
            PricePerUnit = request.PricePerUnit, SecurityDeposit = request.SecurityDeposit, CleaningCharge = request.CleaningCharge,
            AdditionalCharge = request.AdditionalCharge, RequiresApproval = request.RequiresApproval,
            AdvanceBookingDaysLimit = request.AdvanceBookingDaysLimit, CancellationHoursBeforeStart = request.CancellationHoursBeforeStart,
            IsActive = true
        };
        await _context.Facilities.AddAsync(facility, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Facilities", nameof(Facility), facility.Id.ToString(),
            newValues: new { facility.Name, facility.Type }, ct: ct);
        return facility.Id;
    }

    public async Task Handle(UpdateFacilityCommand request, CancellationToken ct)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Facility), request.Id);

        facility.Name = request.Name;
        facility.Type = request.Type;
        facility.Description = request.Description;
        facility.ImageUrl = request.ImageUrl;
        facility.Location = request.Location;
        facility.Capacity = request.Capacity;
        facility.PricingType = request.PricingType;
        facility.PricePerUnit = request.PricePerUnit;
        facility.SecurityDeposit = request.SecurityDeposit;
        facility.CleaningCharge = request.CleaningCharge;
        facility.AdditionalCharge = request.AdditionalCharge;
        facility.RequiresApproval = request.RequiresApproval;
        facility.AdvanceBookingDaysLimit = request.AdvanceBookingDaysLimit;
        facility.CancellationHoursBeforeStart = request.CancellationHoursBeforeStart;
        facility.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Facilities", nameof(Facility), facility.Id.ToString(), ct: ct);
    }

    public async Task Handle(DeleteFacilityCommand request, CancellationToken ct)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Facility), request.Id);

        facility.IsDeleted = true;
        facility.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Facilities", nameof(Facility), facility.Id.ToString(), ct: ct);
    }

    public async Task<int> Handle(AddFacilityBlackoutDateCommand request, CancellationToken ct)
    {
        var exists = await _context.Facilities.AnyAsync(f => f.Id == request.FacilityId, ct);
        if (!exists) throw new NotFoundException(nameof(Facility), request.FacilityId);

        var blackout = new FacilityBlackoutDate { FacilityId = request.FacilityId, BlackoutDate = request.BlackoutDate.Date, Reason = request.Reason };
        await _context.FacilityBlackoutDates.AddAsync(blackout, ct);
        await _context.SaveChangesAsync(ct);
        return blackout.Id;
    }

    public async Task Handle(RemoveFacilityBlackoutDateCommand request, CancellationToken ct)
    {
        var blackout = await _context.FacilityBlackoutDates.FirstOrDefaultAsync(b => b.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(FacilityBlackoutDate), request.Id);
        _context.FacilityBlackoutDates.Remove(blackout);
        await _context.SaveChangesAsync(ct);
    }
}

public class FacilityQueryHandlers :
    IRequestHandler<GetFacilitiesQuery, List<FacilityDto>>,
    IRequestHandler<GetFacilityByIdQuery, FacilityDto>,
    IRequestHandler<GetFacilityBlackoutDatesQuery, List<FacilityBlackoutDateDto>>,
    IRequestHandler<GetFacilityAvailabilityQuery, List<FacilitySlotDto>>
{
    private readonly IApplicationDbContext _context;

    public FacilityQueryHandlers(IApplicationDbContext context) => _context = context;

    private static FacilityDto Project(Facility f) => new()
    {
        Id = f.Id, SocietyId = f.SocietyId, Name = f.Name, Type = f.Type, Description = f.Description, ImageUrl = f.ImageUrl,
        Location = f.Location, Capacity = f.Capacity, PricingType = f.PricingType, PricePerUnit = f.PricePerUnit,
        SecurityDeposit = f.SecurityDeposit, CleaningCharge = f.CleaningCharge, AdditionalCharge = f.AdditionalCharge,
        RequiresApproval = f.RequiresApproval, AdvanceBookingDaysLimit = f.AdvanceBookingDaysLimit,
        CancellationHoursBeforeStart = f.CancellationHoursBeforeStart, IsActive = f.IsActive
    };

    public async Task<List<FacilityDto>> Handle(GetFacilitiesQuery request, CancellationToken ct)
    {
        var query = _context.Facilities.Where(f => f.SocietyId == request.SocietyId);
        if (request.ActiveOnly) query = query.Where(f => f.IsActive);
        return await query.OrderBy(f => f.Name).Select(f => Project(f)).ToListAsync(ct);
    }

    public async Task<FacilityDto> Handle(GetFacilityByIdQuery request, CancellationToken ct)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Facility), request.Id);
        return Project(facility);
    }

    public async Task<List<FacilityBlackoutDateDto>> Handle(GetFacilityBlackoutDatesQuery request, CancellationToken ct) =>
        await _context.FacilityBlackoutDates.Where(b => b.FacilityId == request.FacilityId).OrderBy(b => b.BlackoutDate)
            .Select(b => new FacilityBlackoutDateDto { Id = b.Id, FacilityId = b.FacilityId, BlackoutDate = b.BlackoutDate, Reason = b.Reason })
            .ToListAsync(ct);

    public async Task<List<FacilitySlotDto>> Handle(GetFacilityAvailabilityQuery request, CancellationToken ct)
    {
        var date = request.Date.Date;
        return await _context.FacilityBookings
            .Where(b => b.FacilityId == request.FacilityId && b.BookingDate == date &&
                        b.Status != FacilityBookingStatus.Rejected && b.Status != FacilityBookingStatus.Cancelled)
            .OrderBy(b => b.StartTime)
            .Select(b => new FacilitySlotDto { StartTime = b.StartTime, EndTime = b.EndTime, Status = b.Status })
            .ToListAsync(ct);
    }
}
