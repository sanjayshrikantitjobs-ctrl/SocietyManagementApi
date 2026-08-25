using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Events;

public class EventRsvpDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public int MemberId { get; set; }
    public string MemberName { get; set; } = default!;
    public string MemberPhone { get; set; } = default!;
    public int HeadCount { get; set; }
    public string QrToken { get; set; } = default!;
    public EventRsvpStatus Status { get; set; }
    public int? CheckedInCount { get; set; }
    public DateTime? CheckedInAt { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateOrUpdateRsvpCommand(int EventId, int HeadCount) : IRequest<EventRsvpDto>;

public class CreateOrUpdateRsvpCommandValidator : AbstractValidator<CreateOrUpdateRsvpCommand>
{
    public CreateOrUpdateRsvpCommandValidator()
    {
        RuleFor(x => x.EventId).GreaterThan(0);
        RuleFor(x => x.HeadCount).GreaterThan(0);
    }
}

public record CancelRsvpCommand(int Id) : IRequest<Unit>;

public record CheckInCommand(string QrToken, int ActualHeadCount) : IRequest<EventRsvpDto>;

public class CheckInCommandValidator : AbstractValidator<CheckInCommand>
{
    public CheckInCommandValidator()
    {
        RuleFor(x => x.QrToken).NotEmpty();
        RuleFor(x => x.ActualHeadCount).GreaterThan(0);
    }
}

public class EventRsvpCommandHandlers :
    IRequestHandler<CreateOrUpdateRsvpCommand, EventRsvpDto>,
    IRequestHandler<CancelRsvpCommand, Unit>,
    IRequestHandler<CheckInCommand, EventRsvpDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public EventRsvpCommandHandlers(
        IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<EventRsvpDto> Handle(CreateOrUpdateRsvpCommand request, CancellationToken ct)
    {
        var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Event), request.EventId);
        if (@event.Status != EventStatus.Open)
        {
            throw new ConflictAppException("This event isn't accepting RSVPs right now.");
        }
        if (@event.RsvpDeadline.HasValue && @event.RsvpDeadline.Value < DateTime.UtcNow)
        {
            throw new ConflictAppException("The RSVP deadline for this event has passed.");
        }

        var residency = await _context.Members
            .Where(m => m.UserId == _currentUserService.UserId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .Where(r => !r.IsDeleted && r.MoveOutDate == null)
            .Select(r => new { r.FlatId, r.MemberId })
            .FirstOrDefaultAsync(ct)
            ?? throw new BadRequestAppException("No flat is linked to your account yet — ask your admin to add you as a resident first.");

        var rsvp = await _context.EventRsvps.FirstOrDefaultAsync(
            r => r.EventId == request.EventId && r.FlatId == residency.FlatId && !r.IsDeleted, ct);

        if (rsvp != null && rsvp.Status == EventRsvpStatus.CheckedIn)
        {
            throw new ConflictAppException("You've already checked in for this event — the RSVP can no longer be changed.");
        }

        var otherFlatsHeadCount = await _context.EventRsvps
            .Where(r => r.EventId == request.EventId && !r.IsDeleted && r.Status != EventRsvpStatus.Cancelled && r.FlatId != residency.FlatId)
            .SumAsync(r => (int?)r.HeadCount, ct) ?? 0;

        if (@event.CapacityLimit.HasValue && otherFlatsHeadCount + request.HeadCount > @event.CapacityLimit.Value)
        {
            var remaining = @event.CapacityLimit.Value - otherFlatsHeadCount;
            throw new ConflictAppException(remaining > 0
                ? $"Only {remaining} seat(s) left for this event."
                : "This event is already at capacity.");
        }

        if (rsvp == null)
        {
            rsvp = new EventRsvp
            {
                EventId = request.EventId, FlatId = residency.FlatId, MemberId = residency.MemberId,
                HeadCount = request.HeadCount, QrToken = Guid.NewGuid().ToString("N"), Status = EventRsvpStatus.Registered
            };
            await _context.EventRsvps.AddAsync(rsvp, ct);
        }
        else
        {
            rsvp.HeadCount = request.HeadCount;
            rsvp.Status = EventRsvpStatus.Registered;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Events", nameof(EventRsvp), rsvp.Id.ToString(), ct: ct);
        return await ProjectAsync(rsvp.Id, ct);
    }

    public async Task<Unit> Handle(CancelRsvpCommand request, CancellationToken ct)
    {
        var rsvp = await _context.EventRsvps.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(EventRsvp), request.Id);
        if (rsvp.Status == EventRsvpStatus.CheckedIn)
        {
            throw new ConflictAppException("This flat has already checked in — the RSVP can no longer be cancelled.");
        }

        rsvp.Status = EventRsvpStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Events", nameof(EventRsvp), rsvp.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<EventRsvpDto> Handle(CheckInCommand request, CancellationToken ct)
    {
        var rsvp = await _context.EventRsvps.FirstOrDefaultAsync(r => r.QrToken == request.QrToken && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(EventRsvp), request.QrToken);

        if (rsvp.Status == EventRsvpStatus.Cancelled)
        {
            throw new ConflictAppException("This RSVP was cancelled and can't be checked in.");
        }
        if (rsvp.Status == EventRsvpStatus.CheckedIn)
        {
            throw new ConflictAppException("This flat has already checked in.");
        }

        rsvp.Status = EventRsvpStatus.CheckedIn;
        rsvp.CheckedInCount = request.ActualHeadCount;
        rsvp.CheckedInAt = DateTime.UtcNow;
        rsvp.CheckedInByUserId = _currentUserService.UserId;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Approve, "Events", nameof(EventRsvp), rsvp.Id.ToString(), ct: ct);
        return await ProjectAsync(rsvp.Id, ct);
    }

    private async Task<EventRsvpDto> ProjectAsync(int id, CancellationToken ct) =>
        await Project(_context.EventRsvps.Where(r => r.Id == id)).FirstAsync(ct);

    private static IQueryable<EventRsvpDto> Project(IQueryable<EventRsvp> query) =>
        query.Select(r => new EventRsvpDto
        {
            Id = r.Id, EventId = r.EventId, FlatId = r.FlatId, FlatNumber = r.Flat.FlatNumber, MemberId = r.MemberId,
            MemberName = r.Member.FirstName + " " + r.Member.LastName, MemberPhone = r.Member.Phone,
            HeadCount = r.HeadCount, QrToken = r.QrToken, Status = r.Status,
            CheckedInCount = r.CheckedInCount, CheckedInAt = r.CheckedInAt
        });
}

// ---- Queries -------------------------------------------------------------------
public record GetMyRsvpQuery(int EventId) : IRequest<EventRsvpDto?>;

public record GetRsvpsForEventQuery(
    int EventId, string? Search = null, string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<EventRsvpDto>>;

/// <summary>Powers the phone-camera-landing check-in confirmation page —
/// shown before the admin taps Confirm, so the QrToken lookup used by
/// CheckInCommand needs a read-only counterpart here.</summary>
public record GetRsvpByTokenQuery(string QrToken) : IRequest<EventRsvpDto>;

public class EventRsvpQueryHandlers :
    IRequestHandler<GetMyRsvpQuery, EventRsvpDto?>,
    IRequestHandler<GetRsvpsForEventQuery, PaginatedResult<EventRsvpDto>>,
    IRequestHandler<GetRsvpByTokenQuery, EventRsvpDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public EventRsvpQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private static IQueryable<EventRsvpDto> Project(IQueryable<EventRsvp> query) =>
        query.Select(r => new EventRsvpDto
        {
            Id = r.Id, EventId = r.EventId, FlatId = r.FlatId, FlatNumber = r.Flat.FlatNumber, MemberId = r.MemberId,
            MemberName = r.Member.FirstName + " " + r.Member.LastName, MemberPhone = r.Member.Phone,
            HeadCount = r.HeadCount, QrToken = r.QrToken, Status = r.Status,
            CheckedInCount = r.CheckedInCount, CheckedInAt = r.CheckedInAt
        });

    public async Task<EventRsvpDto?> Handle(GetMyRsvpQuery request, CancellationToken ct)
    {
        var flatId = await _context.Members
            .Where(m => m.UserId == _currentUserService.UserId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .Where(r => !r.IsDeleted && r.MoveOutDate == null)
            .Select(r => (int?)r.FlatId)
            .FirstOrDefaultAsync(ct);

        if (!flatId.HasValue) return null;

        return await Project(_context.EventRsvps
                .Where(r => r.EventId == request.EventId && r.FlatId == flatId && !r.IsDeleted))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResult<EventRsvpDto>> Handle(GetRsvpsForEventQuery request, CancellationToken ct)
    {
        var query = _context.EventRsvps.Where(r => r.EventId == request.EventId && !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(r => r.Flat.FlatNumber.ToLower().Contains(term)
                || r.Member.FirstName.ToLower().Contains(term) || r.Member.LastName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("member", false) => query.OrderBy(r => r.Member.FirstName).ThenBy(r => r.Member.LastName),
            ("member", true) => query.OrderByDescending(r => r.Member.FirstName).ThenByDescending(r => r.Member.LastName),
            ("headcount", false) => query.OrderBy(r => r.HeadCount),
            ("headcount", true) => query.OrderByDescending(r => r.HeadCount),
            ("status", false) => query.OrderBy(r => r.Status),
            ("status", true) => query.OrderByDescending(r => r.Status),
            ("flat", true) => query.OrderByDescending(r => r.Flat.Id),
            _ => query.OrderBy(r => r.Flat.Id)
        };

        var items = await Project(query)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<EventRsvpDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<EventRsvpDto> Handle(GetRsvpByTokenQuery request, CancellationToken ct) =>
        await Project(_context.EventRsvps.Where(r => r.QrToken == request.QrToken && !r.IsDeleted)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(EventRsvp), request.QrToken);
}
