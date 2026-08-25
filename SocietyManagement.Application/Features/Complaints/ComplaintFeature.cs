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

namespace SocietyManagement.Application.Features.Complaints;

// This namespace (Features.Complaints) sits next to Features.Roles and
// Features.Permissions (the Roles & Permissions management feature) — C#
// finds those sibling namespaces via enclosing-namespace lookup before it
// ever consults a `using` directive (aliased or not), so an unqualified
// "Permissions"/"Roles" here resolves to the wrong thing. Every reference
// below is fully qualified against Shared.Constants instead. No other
// Application handler references Permissions/Roles directly (only
// Controllers do, in a different namespace tree), so this collision has
// never come up before.

public class ComplaintDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public int RaisedByUserId { get; set; }
    public string RaisedByName { get; set; } = default!;
    public ComplaintCategory Category { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public ComplaintPriority Priority { get; set; }
    public ComplaintStatus Status { get; set; }
    public string? PhotoUrl { get; set; }
    public int? AssignedStaffId { get; set; }
    public string? AssignedStaffName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? InProgressAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ReopenReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ComplaintKpisDto
{
    public int Open { get; set; }
    public int Assigned { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateComplaintCommand(
    int FlatId, ComplaintCategory Category, string Title, string Description, ComplaintPriority Priority,
    string? PhotoUrl) : IRequest<int>;

public class CreateComplaintCommandValidator : AbstractValidator<CreateComplaintCommand>
{
    public CreateComplaintCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}

public record UpdateComplaintCommand(
    int Id, ComplaintCategory Category, string Title, string Description, ComplaintPriority Priority,
    string? PhotoUrl) : IRequest<Unit>;

public class UpdateComplaintCommandValidator : AbstractValidator<UpdateComplaintCommand>
{
    public UpdateComplaintCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}

public record DeleteComplaintCommand(int Id) : IRequest<Unit>;

public record AssignComplaintCommand(int Id, int StaffId) : IRequest<Unit>;

public class AssignComplaintCommandValidator : AbstractValidator<AssignComplaintCommand>
{
    public AssignComplaintCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.StaffId).GreaterThan(0);
    }
}

public record StartProgressCommand(int Id) : IRequest<Unit>;

public record ResolveComplaintCommand(int Id, string ResolutionNotes) : IRequest<Unit>;

public class ResolveComplaintCommandValidator : AbstractValidator<ResolveComplaintCommand>
{
    public ResolveComplaintCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ResolutionNotes).NotEmpty().MaximumLength(1000);
    }
}

public record CloseComplaintCommand(int Id) : IRequest<Unit>;

public record ReopenComplaintCommand(int Id, string Reason) : IRequest<Unit>;

public class ReopenComplaintCommandValidator : AbstractValidator<ReopenComplaintCommand>
{
    public ReopenComplaintCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Guard-clause state machine mirroring VisitorVisitFeature's shape:
/// fetch → check current status equals the one allowed prior state → throw
/// ConflictAppException if not → mutate → save → audit → notify. Update/
/// Delete/Close/Reopen additionally enforce "raiser or Complaints.Manage" —
/// the one piece of authorization that can't be expressed by the
/// controller-level [HasPermission] attribute alone, since it depends on
/// resource ownership, not just the caller's role.</summary>
public class ComplaintCommandHandlers :
    IRequestHandler<CreateComplaintCommand, int>,
    IRequestHandler<UpdateComplaintCommand, Unit>,
    IRequestHandler<DeleteComplaintCommand, Unit>,
    IRequestHandler<AssignComplaintCommand, Unit>,
    IRequestHandler<StartProgressCommand, Unit>,
    IRequestHandler<ResolveComplaintCommand, Unit>,
    IRequestHandler<CloseComplaintCommand, Unit>,
    IRequestHandler<ReopenComplaintCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public ComplaintCommandHandlers(
        IApplicationDbContext context, ICurrentUserService currentUserService,
        IAuditService auditService, INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public async Task<int> Handle(CreateComplaintCommand request, CancellationToken ct)
    {
        var flat = await _context.Flats
            .Where(f => f.Id == request.FlatId && !f.IsDeleted)
            .Select(f => new { f.Id, f.FlatNumber, SocietyId = f.Floor.Wing.Building.SocietyId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Flat), request.FlatId);

        if (!_currentUserService.HasPermission(SocietyManagement.Shared.Constants.Permissions.Complaints.Manage))
        {
            await EnsureCurrentUserResidesAtAsync(request.FlatId, ct);
        }

        var callerId = _currentUserService.UserId!.Value;
        var callerName = await _context.Users
            .Where(u => u.Id == callerId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        var complaint = new Complaint
        {
            SocietyId = flat.SocietyId, FlatId = request.FlatId, RaisedByUserId = callerId, RaisedByName = callerName,
            Category = request.Category, Title = request.Title, Description = request.Description,
            Priority = request.Priority, PhotoUrl = request.PhotoUrl, Status = ComplaintStatus.Open
        };
        await _context.Complaints.AddAsync(complaint, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);

        await _notificationService.SendToRoleAsync(SocietyManagement.Shared.Constants.Roles.Admin, "ComplaintRaised",
            new { complaintId = complaint.Id, flatNumber = flat.FlatNumber, title = complaint.Title, priority = complaint.Priority }, ct);

        return complaint.Id;
    }

    public async Task<Unit> Handle(UpdateComplaintCommand request, CancellationToken ct)
    {
        var complaint = await GetComplaintAsync(request.Id, ct);
        EnsureRaiserOrManage(complaint);
        if (complaint.Status != ComplaintStatus.Open)
        {
            throw new ConflictAppException("Only an open complaint can be edited.");
        }

        complaint.Category = request.Category;
        complaint.Title = request.Title;
        complaint.Description = request.Description;
        complaint.Priority = request.Priority;
        complaint.PhotoUrl = request.PhotoUrl;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteComplaintCommand request, CancellationToken ct)
    {
        var complaint = await GetComplaintAsync(request.Id, ct);
        EnsureRaiserOrManage(complaint);
        if (complaint.Status != ComplaintStatus.Open)
        {
            throw new ConflictAppException("Only an open complaint can be deleted.");
        }

        complaint.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(AssignComplaintCommand request, CancellationToken ct)
    {
        var complaint = await GetComplaintAsync(request.Id, ct);
        if (complaint.Status != ComplaintStatus.Open)
        {
            throw new ConflictAppException("Only an open complaint can be assigned.");
        }
        if (!await _context.Staff.AnyAsync(s => s.Id == request.StaffId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Staff), request.StaffId);
        }

        complaint.AssignedStaffId = request.StaffId;
        complaint.AssignedAt = DateTime.UtcNow;
        complaint.AssignedByUserId = _currentUserService.UserId;
        complaint.Status = ComplaintStatus.Assigned;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);
        await _notificationService.SendToUserAsync(complaint.RaisedByUserId, "ComplaintAssigned",
            new { complaintId = complaint.Id, title = complaint.Title }, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(StartProgressCommand request, CancellationToken ct)
    {
        var complaint = await GetComplaintAsync(request.Id, ct);
        if (complaint.Status != ComplaintStatus.Assigned)
        {
            throw new ConflictAppException("Only an assigned complaint can be moved to in progress.");
        }

        complaint.InProgressAt = DateTime.UtcNow;
        complaint.Status = ComplaintStatus.InProgress;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);
        await _notificationService.SendToUserAsync(complaint.RaisedByUserId, "ComplaintInProgress",
            new { complaintId = complaint.Id, title = complaint.Title }, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ResolveComplaintCommand request, CancellationToken ct)
    {
        var complaint = await GetComplaintAsync(request.Id, ct);
        if (complaint.Status != ComplaintStatus.InProgress)
        {
            throw new ConflictAppException("Only an in-progress complaint can be resolved.");
        }

        complaint.ResolvedAt = DateTime.UtcNow;
        complaint.ResolvedByUserId = _currentUserService.UserId;
        complaint.ResolutionNotes = request.ResolutionNotes;
        complaint.Status = ComplaintStatus.Resolved;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);
        await _notificationService.SendToUserAsync(complaint.RaisedByUserId, "ComplaintResolved",
            new { complaintId = complaint.Id, title = complaint.Title }, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CloseComplaintCommand request, CancellationToken ct)
    {
        var complaint = await GetComplaintAsync(request.Id, ct);
        EnsureRaiserOrManage(complaint);
        if (complaint.Status != ComplaintStatus.Resolved)
        {
            throw new ConflictAppException("Only a resolved complaint can be closed.");
        }

        complaint.ClosedAt = DateTime.UtcNow;
        complaint.ClosedByUserId = _currentUserService.UserId;
        complaint.Status = ComplaintStatus.Closed;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ReopenComplaintCommand request, CancellationToken ct)
    {
        var complaint = await GetComplaintAsync(request.Id, ct);
        EnsureRaiserOrManage(complaint);
        if (complaint.Status != ComplaintStatus.Resolved)
        {
            throw new ConflictAppException("Only a resolved complaint can be reopened.");
        }

        complaint.Status = ComplaintStatus.Open;
        complaint.ResolvedAt = null;
        complaint.ResolvedByUserId = null;
        complaint.ReopenReason = request.Reason;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Complaints", nameof(Complaint), complaint.Id.ToString(), ct: ct);
        await _notificationService.SendToRoleAsync(SocietyManagement.Shared.Constants.Roles.Admin, "ComplaintReopened",
            new { complaintId = complaint.Id, title = complaint.Title, reason = request.Reason }, ct);
        return Unit.Value;
    }

    private async Task<Complaint> GetComplaintAsync(int id, CancellationToken ct) =>
        await _context.Complaints.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
        ?? throw new NotFoundException(nameof(Complaint), id);

    private void EnsureRaiserOrManage(Complaint complaint)
    {
        if (complaint.RaisedByUserId != _currentUserService.UserId && !_currentUserService.HasPermission(SocietyManagement.Shared.Constants.Permissions.Complaints.Manage))
        {
            throw new ForbiddenAccessException("Only the person who raised this complaint, or an admin, can do this.");
        }
    }

    /// <summary>Never trust a client-supplied FlatId — re-derive residency
    /// server-side. Mirrors VisitorVisitFeature.EnsureCurrentUserResidesAtAsync.</summary>
    private async Task EnsureCurrentUserResidesAtAsync(int flatId, CancellationToken ct)
    {
        var resides = await _context.IsCurrentResidentOfFlatAsync(_currentUserService.UserId, flatId, ct);

        if (!resides)
        {
            throw new ForbiddenAccessException("You are not a current resident of this flat.");
        }
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetComplaintsQuery(int SocietyId, ComplaintCategory? Category, ComplaintPriority? Priority, string? Search)
    : IRequest<List<ComplaintDto>>;

/// <summary>Added alongside GetComplaintsQuery (not replacing it) — the
/// Kanban board still needs one unpaginated call to bucket every status
/// into columns; this backs the separate List/Table view only.</summary>
public record GetComplaintsPagedQuery(
    int SocietyId, ComplaintCategory? Category, ComplaintPriority? Priority, string? Search,
    string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<ComplaintDto>>;

public record GetComplaintByIdQuery(int Id) : IRequest<ComplaintDto>;

public record GetMyComplaintsQuery : IRequest<List<ComplaintDto>>;

public record GetComplaintKpisQuery(int SocietyId) : IRequest<ComplaintKpisDto>;

public class ComplaintQueryHandlers :
    IRequestHandler<GetComplaintsQuery, List<ComplaintDto>>,
    IRequestHandler<GetComplaintsPagedQuery, PaginatedResult<ComplaintDto>>,
    IRequestHandler<GetComplaintByIdQuery, ComplaintDto>,
    IRequestHandler<GetMyComplaintsQuery, List<ComplaintDto>>,
    IRequestHandler<GetComplaintKpisQuery, ComplaintKpisDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ComplaintQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private static IQueryable<ComplaintDto> ProjectDto(IQueryable<Complaint> query) =>
        query.Select(c => new ComplaintDto
        {
            Id = c.Id, SocietyId = c.SocietyId, FlatId = c.FlatId, FlatNumber = c.Flat.FlatNumber,
            RaisedByUserId = c.RaisedByUserId, RaisedByName = c.RaisedByName, Category = c.Category, Title = c.Title,
            Description = c.Description, Priority = c.Priority, Status = c.Status, PhotoUrl = c.PhotoUrl,
            AssignedStaffId = c.AssignedStaffId,
            AssignedStaffName = c.AssignedStaff != null ? c.AssignedStaff.FirstName + " " + c.AssignedStaff.LastName : null,
            AssignedAt = c.AssignedAt, InProgressAt = c.InProgressAt, ResolvedAt = c.ResolvedAt,
            ResolutionNotes = c.ResolutionNotes, ClosedAt = c.ClosedAt, ReopenReason = c.ReopenReason, CreatedAt = c.CreatedAt
        });

    public async Task<List<ComplaintDto>> Handle(GetComplaintsQuery request, CancellationToken ct)
    {
        var query = _context.Complaints.Where(c => c.SocietyId == request.SocietyId);

        if (request.Category.HasValue) query = query.Where(c => c.Category == request.Category);
        if (request.Priority.HasValue) query = query.Where(c => c.Priority == request.Priority);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(term) || c.Flat.FlatNumber.ToLower().Contains(term));
        }

        return await ProjectDto(query.OrderByDescending(c => c.CreatedAt)).ToListAsync(ct);
    }

    public async Task<PaginatedResult<ComplaintDto>> Handle(GetComplaintsPagedQuery request, CancellationToken ct)
    {
        var query = _context.Complaints.Where(c => c.SocietyId == request.SocietyId);

        if (request.Category.HasValue) query = query.Where(c => c.Category == request.Category);
        if (request.Priority.HasValue) query = query.Where(c => c.Priority == request.Priority);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(term) || c.Flat.FlatNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("title", true) => query.OrderByDescending(c => c.Title),
            ("title", false) => query.OrderBy(c => c.Title),
            ("status", true) => query.OrderByDescending(c => c.Status),
            ("status", false) => query.OrderBy(c => c.Status),
            ("priority", true) => query.OrderByDescending(c => c.Priority),
            ("priority", false) => query.OrderBy(c => c.Priority),
            ("createdat", false) => query.OrderBy(c => c.CreatedAt),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };

        var items = await ProjectDto(query)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<ComplaintDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<ComplaintDto> Handle(GetComplaintByIdQuery request, CancellationToken ct)
    {
        var complaint = await ProjectDto(_context.Complaints.Where(c => c.Id == request.Id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Complaint), request.Id);

        // Every Member has Complaints.View (needed for the admin board's own
        // permission gate), so checking View here would let any resident
        // view any other resident's complaint by guessing an id — Manage is
        // the real "can see everyone's complaints" boundary, same as
        // EnsureRaiserOrManage uses for the command handlers.
        if (complaint.RaisedByUserId != _currentUserService.UserId && !_currentUserService.HasPermission(SocietyManagement.Shared.Constants.Permissions.Complaints.Manage))
        {
            throw new ForbiddenAccessException("You can only view your own complaints.");
        }
        return complaint;
    }

    public async Task<List<ComplaintDto>> Handle(GetMyComplaintsQuery request, CancellationToken ct)
    {
        // Same two-model gap as GetMyFlatsQuery (SocietyManagement.Application/Features/Flats/FlatFeature.cs) —
        // an Owner/Tenant login from the Person/Occupancy flow has no
        // Member row, so the legacy-only lookup silently returned zero
        // flats (and zero complaints) for them.
        var viaMember = _context.Members
            .Where(m => m.UserId == _currentUserService.UserId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .Where(r => !r.IsDeleted && r.MoveOutDate == null)
            .Select(r => r.FlatId);

        var viaPerson = _context.Users
            .Where(u => u.Id == _currentUserService.UserId && u.PersonId != null)
            .SelectMany(u => _context.OccupancyMembers
                .Where(om => om.PersonId == u.PersonId && !om.IsDeleted && om.LeftDate == null))
            .Where(om => !om.FlatOccupancy.IsDeleted && om.FlatOccupancy.EndDate == null)
            .Select(om => om.FlatOccupancy.FlatId);

        var flatIds = await viaMember.Union(viaPerson).ToListAsync(ct);

        var query = _context.Complaints.Where(c => flatIds.Contains(c.FlatId));
        return await ProjectDto(query.OrderByDescending(c => c.CreatedAt)).ToListAsync(ct);
    }

    public async Task<ComplaintKpisDto> Handle(GetComplaintKpisQuery request, CancellationToken ct)
    {
        var counts = await _context.Complaints
            .Where(c => c.SocietyId == request.SocietyId)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(ComplaintStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        return new ComplaintKpisDto
        {
            Open = CountOf(ComplaintStatus.Open), Assigned = CountOf(ComplaintStatus.Assigned),
            InProgress = CountOf(ComplaintStatus.InProgress), Resolved = CountOf(ComplaintStatus.Resolved),
            Closed = CountOf(ComplaintStatus.Closed)
        };
    }
}
