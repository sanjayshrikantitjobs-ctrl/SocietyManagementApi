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

namespace SocietyManagement.Application.Features.Facilities;

public class FacilityBookingDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public int FacilityId { get; set; }
    public string FacilityName { get; set; } = default!;
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public int BookedByUserId { get; set; }
    public string BookedByName { get; set; } = default!;
    public DateTime BookingDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Purpose { get; set; }
    public int GuestCount { get; set; }
    public string? Notes { get; set; }
    public decimal RentalCharge { get; set; }
    public decimal SecurityDepositAmount { get; set; }
    public decimal CleaningChargeAmount { get; set; }
    public decimal AdditionalChargeAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositRefundAmount { get; set; }
    public FacilityPaymentStatus PaymentStatus { get; set; }
    public FacilityBookingStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ==================== Commands ====================

public record CreateFacilityBookingCommand(
    int FacilityId, int FlatId, DateTime BookingDate, TimeSpan StartTime, TimeSpan EndTime,
    string? Purpose, int GuestCount, string? Notes) : IRequest<int>;

public class CreateFacilityBookingCommandValidator : AbstractValidator<CreateFacilityBookingCommand>
{
    public CreateFacilityBookingCommandValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("End time must be after start time.");
        RuleFor(x => x.GuestCount).GreaterThanOrEqualTo(0);
    }
}

public record UpdateFacilityBookingStatusCommand(int Id, FacilityBookingStatus Status, string? RejectionReason) : IRequest;

public record RecordFacilityBookingPaymentCommand(int Id, FacilityPaymentStatus PaymentStatus) : IRequest;

public class FacilityBookingCommandHandlers :
    IRequestHandler<CreateFacilityBookingCommand, int>,
    IRequestHandler<UpdateFacilityBookingStatusCommand>,
    IRequestHandler<RecordFacilityBookingPaymentCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public FacilityBookingCommandHandlers(IApplicationDbContext context, ICurrentUserService currentUser, IAuditService auditService)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    private static decimal CalculateRentalCharge(Facility facility, TimeSpan start, TimeSpan end) => facility.PricingType switch
    {
        FacilityPricingType.PerHour => Math.Round(facility.PricePerUnit * (decimal)(end - start).TotalHours, 2),
        _ => facility.PricePerUnit // PerDay / Lumpsum: one flat charge regardless of the hour window booked within that day.
    };

    public async Task<int> Handle(CreateFacilityBookingCommand request, CancellationToken ct)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == request.FacilityId, ct)
            ?? throw new NotFoundException(nameof(Facility), request.FacilityId);
        if (!facility.IsActive) throw new BadRequestAppException("This facility is not currently available for booking.");

        var flat = await _context.Flats
            .Where(f => f.Id == request.FlatId && !f.IsDeleted)
            .Select(f => new { f.Id, f.FlatNumber, SocietyId = f.Floor.Wing.Building.SocietyId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Flat), request.FlatId);

        // Cross-society isolation: a facility can only ever be booked by a
        // flat in the same society it belongs to — the SocietyScopeFilter
        // can't see this (neither argument is a bound SocietyId), so it's
        // checked by hand here, the same pattern Announcements' by-Id
        // actions use for post-load ownership checks.
        if (facility.SocietyId != flat.SocietyId) throw new NotFoundException(nameof(Facility), request.FacilityId);

        if (!_currentUser.HasPermission(SocietyManagement.Shared.Constants.Permissions.Facilities.Manage))
        {
            await EnsureCurrentUserResidesAtAsync(request.FlatId, ct);
        }

        var bookingDate = request.BookingDate.Date;
        if (bookingDate < DateTime.UtcNow.Date) throw new BadRequestAppException("Cannot book a facility for a past date.");
        if (facility.AdvanceBookingDaysLimit > 0 && bookingDate > DateTime.UtcNow.Date.AddDays(facility.AdvanceBookingDaysLimit))
        {
            throw new BadRequestAppException($"This facility can only be booked up to {facility.AdvanceBookingDaysLimit} day(s) in advance.");
        }

        var isBlackedOut = await _context.FacilityBlackoutDates.AnyAsync(b => b.FacilityId == request.FacilityId && b.BlackoutDate == bookingDate, ct);
        if (isBlackedOut) throw new BadRequestAppException("This facility is blocked for booking on the selected date.");

        var callerId = _currentUser.UserId!.Value;
        var callerName = await _context.Users.Where(u => u.Id == callerId)
            .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct) ?? "Unknown";

        var rentalCharge = CalculateRentalCharge(facility, request.StartTime, request.EndTime);
        var totalAmount = rentalCharge + facility.SecurityDeposit + facility.CleaningCharge + facility.AdditionalCharge;

        // Serializable isolation: the AnyAsync overlap check below and the
        // insert that follows are covered by one range lock per
        // (FacilityId, BookingDate) — see IApplicationDbContext.
        // BeginSerializableTransactionAsync's doc comment for why this is
        // the DB-level guard against two residents double-booking the same
        // slot, not just an application-level race that happens to work
        // most of the time.
        await using var transaction = await _context.BeginSerializableTransactionAsync(ct);
        try
        {
            // The FacilityId+BookingDate+Status filter narrows this to a
            // handful of rows (one facility, one day) and IS translatable by
            // every provider; the actual time-window overlap check then runs
            // in memory, since TimeSpan comparisons aren't reliably
            // translatable across relational providers (SQLite — used by
            // this handler's own tests — can't translate them; this also
            // sidesteps any such gap on the production provider).
            var sameDayBookings = await _context.FacilityBookings
                .Where(b => b.FacilityId == request.FacilityId && b.BookingDate == bookingDate &&
                            b.Status != FacilityBookingStatus.Rejected && b.Status != FacilityBookingStatus.Cancelled)
                .ToListAsync(ct);
            var hasOverlap = sameDayBookings.Any(b => b.StartTime < request.EndTime && b.EndTime > request.StartTime);

            if (hasOverlap) throw new ConflictAppException("This facility is already booked for the selected time slot.");

            var booking = new FacilityBooking
            {
                SocietyId = facility.SocietyId, FacilityId = facility.Id, FlatId = request.FlatId,
                BookedByUserId = callerId, BookedByName = callerName, BookingDate = bookingDate,
                StartTime = request.StartTime, EndTime = request.EndTime, Purpose = request.Purpose,
                GuestCount = request.GuestCount, Notes = request.Notes, RentalCharge = rentalCharge,
                SecurityDepositAmount = facility.SecurityDeposit, CleaningChargeAmount = facility.CleaningCharge,
                AdditionalChargeAmount = facility.AdditionalCharge, TotalAmount = totalAmount,
                Status = facility.RequiresApproval ? FacilityBookingStatus.Pending : FacilityBookingStatus.Approved
            };
            await _context.FacilityBookings.AddAsync(booking, ct);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _auditService.LogAsync(AuditAction.Create, "FacilityBookings", nameof(FacilityBooking), booking.Id.ToString(),
                newValues: new { booking.FacilityId, booking.BookingDate, booking.Status }, ct: ct);

            return booking.Id;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task Handle(UpdateFacilityBookingStatusCommand request, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(request.Id, ct);
        booking.Status = request.Status;
        if (request.Status == FacilityBookingStatus.Rejected || request.Status == FacilityBookingStatus.Cancelled)
        {
            booking.RejectionReason = request.RejectionReason;
        }
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "FacilityBookings", nameof(FacilityBooking), booking.Id.ToString(),
            newValues: new { Status = request.Status.ToString() }, ct: ct);
    }

    public async Task Handle(RecordFacilityBookingPaymentCommand request, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(request.Id, ct);
        booking.PaymentStatus = request.PaymentStatus;
        await _context.SaveChangesAsync(ct);
    }

    private async Task<FacilityBooking> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var booking = await _context.FacilityBookings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(FacilityBooking), id);

        if (_currentUser.SocietyId is { } callerSocietyId && booking.SocietyId != callerSocietyId)
        {
            throw new ForbiddenAccessException("You do not have access to this society's data.");
        }

        return booking;
    }

    private async Task EnsureCurrentUserResidesAtAsync(int flatId, CancellationToken ct)
    {
        var resides = await _context.IsCurrentResidentOfFlatAsync(_currentUser.UserId, flatId, ct);
        if (!resides) throw new ForbiddenAccessException("You are not a current resident of this flat.");
    }
}

// ==================== Queries ====================

public record GetFacilityBookingsQuery(
    int SocietyId, int? FacilityId, FacilityBookingStatus? Status, DateTime? DateFrom, DateTime? DateTo,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FacilityBookingDto>>;

public record GetMyFacilityBookingsQuery(FacilityBookingStatus? Status, int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize)
    : IRequest<PaginatedResult<FacilityBookingDto>>;

public record GetFacilityBookingByIdQuery(int Id) : IRequest<FacilityBookingDto>;

public class FacilityBookingQueryHandlers :
    IRequestHandler<GetFacilityBookingsQuery, PaginatedResult<FacilityBookingDto>>,
    IRequestHandler<GetMyFacilityBookingsQuery, PaginatedResult<FacilityBookingDto>>,
    IRequestHandler<GetFacilityBookingByIdQuery, FacilityBookingDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FacilityBookingQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private static IQueryable<FacilityBookingDto> Project(IQueryable<FacilityBooking> query) => query.Select(b => new FacilityBookingDto
    {
        Id = b.Id, SocietyId = b.SocietyId, FacilityId = b.FacilityId, FacilityName = b.Facility.Name, FlatId = b.FlatId,
        FlatNumber = b.Flat.FlatNumber, BookedByUserId = b.BookedByUserId, BookedByName = b.BookedByName, BookingDate = b.BookingDate,
        StartTime = b.StartTime, EndTime = b.EndTime, Purpose = b.Purpose, GuestCount = b.GuestCount, Notes = b.Notes,
        RentalCharge = b.RentalCharge, SecurityDepositAmount = b.SecurityDepositAmount, CleaningChargeAmount = b.CleaningChargeAmount,
        AdditionalChargeAmount = b.AdditionalChargeAmount, TotalAmount = b.TotalAmount, DepositRefundAmount = b.DepositRefundAmount,
        PaymentStatus = b.PaymentStatus, Status = b.Status, RejectionReason = b.RejectionReason, CreatedAt = b.CreatedAt
    });

    public async Task<PaginatedResult<FacilityBookingDto>> Handle(GetFacilityBookingsQuery request, CancellationToken ct)
    {
        var query = _context.FacilityBookings.Where(b => b.SocietyId == request.SocietyId);
        if (request.FacilityId.HasValue) query = query.Where(b => b.FacilityId == request.FacilityId.Value);
        if (request.Status.HasValue) query = query.Where(b => b.Status == request.Status.Value);
        if (request.DateFrom.HasValue) query = query.Where(b => b.BookingDate >= request.DateFrom.Value.Date);
        if (request.DateTo.HasValue) query = query.Where(b => b.BookingDate <= request.DateTo.Value.Date);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = await Project(query.OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.StartTime))
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PaginatedResult<FacilityBookingDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PaginatedResult<FacilityBookingDto>> Handle(GetMyFacilityBookingsQuery request, CancellationToken ct)
    {
        var flatIds = await _context.GetCurrentResidentFlatIdsAsync(_currentUser.UserId, ct);
        var query = _context.FacilityBookings.Where(b => flatIds.Contains(b.FlatId));
        if (request.Status.HasValue) query = query.Where(b => b.Status == request.Status.Value);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = await Project(query.OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.StartTime))
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PaginatedResult<FacilityBookingDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<FacilityBookingDto> Handle(GetFacilityBookingByIdQuery request, CancellationToken ct)
    {
        var booking = await _context.FacilityBookings.FirstOrDefaultAsync(b => b.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(FacilityBooking), request.Id);

        if (_currentUser.SocietyId is { } callerSocietyId && booking.SocietyId != callerSocietyId)
        {
            throw new ForbiddenAccessException("You do not have access to this society's data.");
        }

        return await Project(_context.FacilityBookings.Where(b => b.Id == request.Id)).FirstAsync(ct);
    }
}
