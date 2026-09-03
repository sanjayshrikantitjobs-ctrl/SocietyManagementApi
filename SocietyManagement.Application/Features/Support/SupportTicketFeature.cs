using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Support;

public class SupportTicketDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string SocietyName { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;
    public SupportTicketStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByName { get; set; }
    public string? ResolutionNotes { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateSupportTicketCommand(string Subject, string Description) : IRequest<int>;

public class CreateSupportTicketCommandValidator : AbstractValidator<CreateSupportTicketCommand>
{
    public CreateSupportTicketCommandValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
    }
}

/// <summary>Super Admin-only (see Permissions.SupportTickets.ManageAll) —
/// moves a ticket through Open/InProgress/Resolved and, on Resolved,
/// notifies the original creator.</summary>
public record UpdateSupportTicketStatusCommand(int Id, SupportTicketStatus Status, string? ResolutionNotes) : IRequest<Unit>;

public class UpdateSupportTicketStatusCommandValidator : AbstractValidator<UpdateSupportTicketStatusCommand>
{
    public UpdateSupportTicketStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class SupportTicketCommandHandlers :
    IRequestHandler<CreateSupportTicketCommand, int>,
    IRequestHandler<UpdateSupportTicketStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;

    public SupportTicketCommandHandlers(
        IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser,
        INotificationService notificationService)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    public async Task<int> Handle(CreateSupportTicketCommand request, CancellationToken ct)
    {
        // Only an Admin or Member (never Super Admin, who has no SocietyId)
        // holds SupportTickets.Create — see DbSeeder's memberPermissionCodes
        // and the Admin grant, so SocietyId is always present here.
        var societyId = _currentUser.SocietyId
            ?? throw new BadRequestAppException("Only a society-scoped user can raise a support ticket.");
        var userId = _currentUser.UserId!.Value;

        var ticket = new SupportTicket
        {
            SocietyId = societyId, CreatedByUserId = userId, Subject = request.Subject, Description = request.Description,
            Status = SupportTicketStatus.Open
        };
        await _context.SupportTickets.AddAsync(ticket, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Support", nameof(SupportTicket), ticket.Id.ToString(), ct: ct);

        var societyName = await _context.Societies.Where(s => s.Id == societyId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        await _notificationService.SendToRoleAsync(SocietyManagement.Shared.Constants.Roles.SuperAdmin, "SupportTicketCreated",
            new { ticketId = ticket.Id, subject = ticket.Subject, societyName }, ct);

        return ticket.Id;
    }

    public async Task<Unit> Handle(UpdateSupportTicketStatusCommand request, CancellationToken ct)
    {
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(SupportTicket), request.Id);

        ticket.Status = request.Status;
        if (request.Status == SupportTicketStatus.Resolved)
        {
            ticket.ResolvedAt = DateTime.UtcNow;
            ticket.ResolvedByUserId = _currentUser.UserId;
            ticket.ResolutionNotes = request.ResolutionNotes;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Support", nameof(SupportTicket), ticket.Id.ToString(), ct: ct);

        if (request.Status == SupportTicketStatus.Resolved)
        {
            await _notificationService.SendToUserAsync(ticket.CreatedByUserId, "SupportTicketResolved",
                new { ticketId = ticket.Id, subject = ticket.Subject }, ct);
        }

        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
/// <summary>Every ticket the current user themselves raised, across time —
/// mirrors GetMyWaterTankerCollectionsQuery's "no params, resolved from
/// identity" shape.</summary>
public record GetMyTicketsQuery : IRequest<List<SupportTicketDto>>;

/// <summary>Super Admin-only — every ticket across every society, since
/// Super Admin has no SocietyId to scope by (see SocietyScopeFilter).</summary>
public record GetAllTicketsQuery(SupportTicketStatus? Status, int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize)
    : IRequest<PaginatedResult<SupportTicketDto>>;

public class SupportTicketQueryHandlers :
    IRequestHandler<GetMyTicketsQuery, List<SupportTicketDto>>,
    IRequestHandler<GetAllTicketsQuery, PaginatedResult<SupportTicketDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SupportTicketQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private static SupportTicketDto Project(SupportTicket t) => new()
    {
        Id = t.Id, SocietyId = t.SocietyId, SocietyName = t.Society.Name,
        CreatedByName = t.CreatedByUser.FirstName + " " + t.CreatedByUser.LastName,
        Subject = t.Subject, Description = t.Description, Status = t.Status, CreatedAt = t.CreatedAt,
        ResolvedAt = t.ResolvedAt,
        ResolvedByName = t.ResolvedByUser != null ? t.ResolvedByUser.FirstName + " " + t.ResolvedByUser.LastName : null,
        ResolutionNotes = t.ResolutionNotes
    };

    public async Task<List<SupportTicketDto>> Handle(GetMyTicketsQuery request, CancellationToken ct) =>
        await _context.SupportTickets
            // Project() isn't translatable to SQL (it's a separate static method, not
            // an inline lambda EF Core can decompile), so it runs client-side against
            // materialized entities — without these Includes, Society/CreatedByUser/
            // ResolvedByUser are never loaded and Project() NullReferenceExceptions
            // the moment it dereferences them.
            .Include(t => t.Society)
            .Include(t => t.CreatedByUser)
            .Include(t => t.ResolvedByUser)
            .Where(t => !t.IsDeleted && t.CreatedByUserId == _currentUser.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => Project(t))
            .ToListAsync(ct);

    public async Task<PaginatedResult<SupportTicketDto>> Handle(GetAllTicketsQuery request, CancellationToken ct)
    {
        var query = _context.SupportTickets
            .Include(t => t.Society)
            .Include(t => t.CreatedByUser)
            .Include(t => t.ResolvedByUser)
            .Where(t => !t.IsDeleted);
        if (request.Status.HasValue) query = query.Where(t => t.Status == request.Status);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => Project(t))
            .ToListAsync(ct);

        return new PaginatedResult<SupportTicketDto>(items, totalCount, pageNumber, pageSize);
    }
}
