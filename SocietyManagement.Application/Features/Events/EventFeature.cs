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

public class EventDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public int? FestivalId { get; set; }
    public string? FestivalName { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime EventDateTime { get; set; }
    public string? Venue { get; set; }
    public int? CapacityLimit { get; set; }
    public DateTime? RsvpDeadline { get; set; }
    public EventStatus Status { get; set; }
}

/// <summary>The direct answer to "how many plates do we need" — computed
/// from EventRsvp rows, nothing stored redundantly on Event itself.</summary>
public class EventCapacitySummaryDto
{
    public int EventId { get; set; }
    public int? CapacityLimit { get; set; }
    public int TotalRegistered { get; set; }
    public int TotalCheckedIn { get; set; }
    public int? RemainingSeats { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateEventCommand(
    int SocietyId, int? FestivalId, string Name, string? Description, DateTime EventDateTime,
    string? Venue, int? CapacityLimit, DateTime? RsvpDeadline) : IRequest<int>;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CapacityLimit).GreaterThan(0).When(x => x.CapacityLimit.HasValue);
    }
}

public record UpdateEventCommand(
    int Id, string Name, string? Description, DateTime EventDateTime,
    string? Venue, int? CapacityLimit, DateTime? RsvpDeadline) : IRequest<Unit>;

public class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CapacityLimit).GreaterThan(0).When(x => x.CapacityLimit.HasValue);
    }
}

public record DeleteEventCommand(int Id) : IRequest<Unit>;
public record OpenEventCommand(int Id) : IRequest<Unit>;
public record CloseEventCommand(int Id) : IRequest<Unit>;
public record CompleteEventCommand(int Id) : IRequest<Unit>;
public record CancelEventCommand(int Id) : IRequest<Unit>;

/// <summary>Transition commands follow the same shape as
/// FestivalExpenseFeature's Submit/Approve/Reject state machine.</summary>
public class EventCommandHandlers :
    IRequestHandler<CreateEventCommand, int>,
    IRequestHandler<UpdateEventCommand, Unit>,
    IRequestHandler<DeleteEventCommand, Unit>,
    IRequestHandler<OpenEventCommand, Unit>,
    IRequestHandler<CloseEventCommand, Unit>,
    IRequestHandler<CompleteEventCommand, Unit>,
    IRequestHandler<CancelEventCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public EventCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateEventCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }
        if (request.FestivalId.HasValue &&
            !await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.FestivalId.Value);
        }

        var @event = new Event
        {
            SocietyId = request.SocietyId, FestivalId = request.FestivalId, Name = request.Name,
            Description = request.Description, EventDateTime = request.EventDateTime, Venue = request.Venue,
            CapacityLimit = request.CapacityLimit, RsvpDeadline = request.RsvpDeadline, Status = EventStatus.Draft
        };
        await _context.Events.AddAsync(@event, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Events", nameof(Event), @event.Id.ToString(), ct: ct);
        return @event.Id;
    }

    public async Task<Unit> Handle(UpdateEventCommand request, CancellationToken ct)
    {
        var @event = await GetEventAsync(request.Id, ct);
        if (@event.Status is EventStatus.Completed or EventStatus.Cancelled)
        {
            throw new ConflictAppException("Completed or cancelled events can no longer be edited.");
        }

        @event.Name = request.Name;
        @event.Description = request.Description;
        @event.EventDateTime = request.EventDateTime;
        @event.Venue = request.Venue;
        @event.CapacityLimit = request.CapacityLimit;
        @event.RsvpDeadline = request.RsvpDeadline;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Events", nameof(Event), @event.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteEventCommand request, CancellationToken ct)
    {
        var @event = await GetEventAsync(request.Id, ct);
        if (await _context.EventRsvps.AnyAsync(r => r.EventId == @event.Id && !r.IsDeleted, ct))
        {
            throw new ConflictAppException("Cannot delete an event that already has RSVPs. Cancel it instead.");
        }

        @event.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Events", nameof(Event), @event.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(OpenEventCommand request, CancellationToken ct)
    {
        var @event = await GetEventAsync(request.Id, ct);
        if (@event.Status != EventStatus.Draft)
        {
            throw new ConflictAppException("Only a draft event can be opened for RSVPs.");
        }
        @event.Status = EventStatus.Open;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Events", nameof(Event), @event.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CloseEventCommand request, CancellationToken ct)
    {
        var @event = await GetEventAsync(request.Id, ct);
        if (@event.Status != EventStatus.Open)
        {
            throw new ConflictAppException("Only an open event can be closed to new RSVPs.");
        }
        @event.Status = EventStatus.Closed;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Events", nameof(Event), @event.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CompleteEventCommand request, CancellationToken ct)
    {
        var @event = await GetEventAsync(request.Id, ct);
        if (@event.Status is not (EventStatus.Open or EventStatus.Closed))
        {
            throw new ConflictAppException("Only an open or closed event can be marked completed.");
        }
        @event.Status = EventStatus.Completed;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Events", nameof(Event), @event.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CancelEventCommand request, CancellationToken ct)
    {
        var @event = await GetEventAsync(request.Id, ct);
        if (@event.Status is EventStatus.Completed or EventStatus.Cancelled)
        {
            throw new ConflictAppException("This event is already closed out.");
        }
        @event.Status = EventStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Events", nameof(Event), @event.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    private async Task<Event> GetEventAsync(int id, CancellationToken ct) =>
        await _context.Events.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
        ?? throw new NotFoundException(nameof(Event), id);
}

// ---- Queries -------------------------------------------------------------------
public record GetEventsQuery(
    int SocietyId, EventStatus? Status, int? FestivalId,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<EventDto>>;

public record GetEventByIdQuery(int Id) : IRequest<EventDto>;

public record GetEventCapacitySummaryQuery(int EventId) : IRequest<EventCapacitySummaryDto>;

public class EventQueryHandlers :
    IRequestHandler<GetEventsQuery, PaginatedResult<EventDto>>,
    IRequestHandler<GetEventByIdQuery, EventDto>,
    IRequestHandler<GetEventCapacitySummaryQuery, EventCapacitySummaryDto>
{
    private readonly IApplicationDbContext _context;

    public EventQueryHandlers(IApplicationDbContext context) => _context = context;

    private static IQueryable<EventDto> Project(IQueryable<Event> query) =>
        query.Select(e => new EventDto
        {
            Id = e.Id, SocietyId = e.SocietyId, FestivalId = e.FestivalId,
            FestivalName = e.Festival != null ? e.Festival.Name : null, Name = e.Name, Description = e.Description,
            EventDateTime = e.EventDateTime, Venue = e.Venue, CapacityLimit = e.CapacityLimit,
            RsvpDeadline = e.RsvpDeadline, Status = e.Status
        });

    public async Task<PaginatedResult<EventDto>> Handle(GetEventsQuery request, CancellationToken ct)
    {
        var query = _context.Events.Where(e => e.SocietyId == request.SocietyId);

        if (request.Status.HasValue) query = query.Where(e => e.Status == request.Status);
        if (request.FestivalId.HasValue) query = query.Where(e => e.FestivalId == request.FestivalId);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await Project(query.OrderByDescending(e => e.EventDateTime))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<EventDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<EventDto> Handle(GetEventByIdQuery request, CancellationToken ct) =>
        await Project(_context.Events.Where(e => e.Id == request.Id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Event), request.Id);

    public async Task<EventCapacitySummaryDto> Handle(GetEventCapacitySummaryQuery request, CancellationToken ct)
    {
        var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Event), request.EventId);

        var activeRsvps = await _context.EventRsvps
            .Where(r => r.EventId == request.EventId && !r.IsDeleted && r.Status != EventRsvpStatus.Cancelled)
            .Select(r => new { r.HeadCount, r.CheckedInCount })
            .ToListAsync(ct);

        var totalRegistered = activeRsvps.Sum(r => r.HeadCount);
        var totalCheckedIn = activeRsvps.Sum(r => r.CheckedInCount ?? 0);

        return new EventCapacitySummaryDto
        {
            EventId = @event.Id, CapacityLimit = @event.CapacityLimit, TotalRegistered = totalRegistered,
            TotalCheckedIn = totalCheckedIn,
            RemainingSeats = @event.CapacityLimit.HasValue ? @event.CapacityLimit.Value - totalRegistered : null
        };
    }
}
