using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Helpers;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Visitors;

public class VisitorVisitDto
{
    public int Id { get; set; }
    public int VisitorId { get; set; }
    public string VisitorName { get; set; } = default!;
    public string VisitorMobile { get; set; } = default!;
    public string? VisitorPhotoUrl { get; set; }
    public string? VisitorVehicleNumber { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public int PurposeId { get; set; }
    public string PurposeName { get; set; } = default!;
    public int GateId { get; set; }
    public string GateName { get; set; } = default!;
    public int NumberOfVisitors { get; set; }
    public VisitorVisitStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
}

// ---- Commands ----------------------------------------------------------------
/// <summary>Either VisitorId (repeat visitor) or the New* fields (first-time
/// visitor) must be supplied — validated in the handler since it's a
/// cross-field rule FluentValidation would express awkwardly here.</summary>
public record CreateVisitCommand(
    int? VisitorId, string? NewVisitorName, string? NewVisitorMobile, string? NewVisitorPhotoUrl,
    string? NewVisitorVehicleNumber, string? NewVisitorVehicleType,
    int FlatId, int PurposeId, int GateId, int NumberOfVisitors) : IRequest<VisitorVisitDto>;

public class CreateVisitCommandValidator : AbstractValidator<CreateVisitCommand>
{
    public CreateVisitCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.PurposeId).GreaterThan(0);
        RuleFor(x => x.GateId).GreaterThan(0);
        RuleFor(x => x.NumberOfVisitors).GreaterThan(0);
        RuleFor(x => x).Must(x => x.VisitorId.HasValue || !string.IsNullOrWhiteSpace(x.NewVisitorName))
            .WithMessage("Either an existing visitor or a name for a new visitor is required.");
    }
}

public record ApproveVisitCommand(int Id) : IRequest<Unit>;
public record RejectVisitCommand(int Id, string? Reason) : IRequest<Unit>;
public record CheckInVisitCommand(int Id) : IRequest<Unit>;
public record CheckOutVisitCommand(int Id) : IRequest<Unit>;
public record CancelVisitCommand(int Id) : IRequest<Unit>;

/// <summary>The no-login counterparts of Approve/RejectVisitCommand, driven by
/// the token in the WhatsApp approval link rather than a signed-in resident —
/// not every flat has a resident user account. Never exposed except through
/// VisitorApprovalPublicController's [AllowAnonymous] endpoints.</summary>
public record ApproveVisitByTokenCommand(string Token) : IRequest<Unit>;
public record RejectVisitByTokenCommand(string Token, string? Reason) : IRequest<Unit>;

/// <summary>Not exposed on any controller directly by user action — only the
/// background expiry job dispatches this.</summary>
public record ExpireVisitRequestCommand(int Id) : IRequest<Unit>;

public class VisitorVisitCommandHandlers :
    IRequestHandler<CreateVisitCommand, VisitorVisitDto>,
    IRequestHandler<ApproveVisitCommand, Unit>,
    IRequestHandler<RejectVisitCommand, Unit>,
    IRequestHandler<ApproveVisitByTokenCommand, Unit>,
    IRequestHandler<RejectVisitByTokenCommand, Unit>,
    IRequestHandler<CheckInVisitCommand, Unit>,
    IRequestHandler<CheckOutVisitCommand, Unit>,
    IRequestHandler<CancelVisitCommand, Unit>,
    IRequestHandler<ExpireVisitRequestCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IAppUrlService _appUrlService;
    private readonly ILogger<VisitorVisitCommandHandlers> _logger;

    public VisitorVisitCommandHandlers(
        IApplicationDbContext context, IAuditService auditService,
        ICurrentUserService currentUserService, INotificationService notificationService,
        IWhatsAppService whatsAppService, IAppUrlService appUrlService,
        ILogger<VisitorVisitCommandHandlers> logger)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _whatsAppService = whatsAppService;
        _appUrlService = appUrlService;
        _logger = logger;
    }

    public async Task<VisitorVisitDto> Handle(CreateVisitCommand request, CancellationToken ct)
    {
        var flat = await _context.Flats
            .Where(f => f.Id == request.FlatId && !f.IsDeleted)
            .Select(f => new { f.Id, f.FlatNumber, SocietyId = f.Floor.Wing.Building.SocietyId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Flat), request.FlatId);

        var gate = await _context.Gates.FirstOrDefaultAsync(g => g.Id == request.GateId && g.SocietyId == flat.SocietyId && !g.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Gate), request.GateId);
        var purpose = await _context.VisitorPurposes.FirstOrDefaultAsync(p => p.Id == request.PurposeId && p.SocietyId == flat.SocietyId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(VisitorPurpose), request.PurposeId);

        int visitorId;
        if (request.VisitorId.HasValue)
        {
            if (!await _context.Visitors.AnyAsync(v => v.Id == request.VisitorId && v.SocietyId == flat.SocietyId && !v.IsDeleted, ct))
            {
                throw new NotFoundException(nameof(Visitor), request.VisitorId.Value);
            }
            visitorId = request.VisitorId.Value;
        }
        else
        {
            var visitor = new Visitor
            {
                SocietyId = flat.SocietyId, Name = request.NewVisitorName!, MobileNumber = request.NewVisitorMobile ?? "",
                PhotoUrl = request.NewVisitorPhotoUrl, VehicleNumber = request.NewVisitorVehicleNumber,
                VehicleType = request.NewVisitorVehicleType
            };
            await _context.Visitors.AddAsync(visitor, ct);
            await _context.SaveChangesAsync(ct);
            visitorId = visitor.Id;
        }

        var watchmanUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAppException("Could not identify the requesting user.");

        var visit = new VisitorVisit
        {
            SocietyId = flat.SocietyId, VisitorId = visitorId, FlatId = flat.Id, PurposeId = purpose.Id,
            GateId = gate.Id, NumberOfVisitors = request.NumberOfVisitors, CreatedByUserId = watchmanUserId,
            RequestedAt = DateTime.UtcNow,
            Status = purpose.RequiresApproval ? VisitorVisitStatus.PendingApproval : VisitorVisitStatus.Approved,
            ApprovalToken = purpose.RequiresApproval ? Guid.NewGuid().ToString("N") : null
        };
        if (!purpose.RequiresApproval)
        {
            visit.ApprovedAt = DateTime.UtcNow;
        }

        await _context.VisitorVisits.AddAsync(visit, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Visitors", nameof(VisitorVisit), visit.Id.ToString(), ct: ct);

        if (visit.Status == VisitorVisitStatus.PendingApproval)
        {
            var residentUserIds = await GetCurrentResidentUserIdsAsync(flat.Id, ct);
            var dto = await ProjectAsync(visit.Id, ct);
            foreach (var userId in residentUserIds)
            {
                await _notificationService.SendToUserAsync(userId, "VisitorApprovalRequested", dto, ct);
            }

            // Additive to the SignalR push above, not a replacement — this reaches
            // the flat's primary owner/tenant even when no resident user account
            // (and so no SignalR connection) exists for the flat at all.
            await SendWhatsAppApprovalRequestAsync(flat.Id, visit, dto, ct);
        }

        return await ProjectAsync(visit.Id, ct);
    }

    /// <summary>Two resident data models coexist in this codebase — the newer
    /// FlatOccupancy/OccupancyMember/Person model (what the Residents > Owners
    /// UI actually manages, including a dedicated WhatsAppNumber field) and the
    /// older FlatResidency/Member model (what MaintenanceBillFeature/
    /// FestivalContributionFeature's own inline lookups still use, and which has
    /// no WhatsApp-specific field at all). A flat's residents typically live in
    /// only one of the two, so this checks the current one first, then the
    /// legacy one, before finally falling back to Flat.OwnerPhone — a stale
    /// snapshot from whenever the flat record was first created, which is what
    /// was silently winning for flat 1602: it has no FlatResidency row (its
    /// residents were entered via Owner Occupancy), so the old resolver fell
    /// straight through to that leftover value.</summary>
    private async Task<string?> ResolveFlatOwnerPhoneAsync(int flatId, CancellationToken ct)
    {
        // Tenant preferred over Owner when a flat has both open episodes — the
        // tenant is who's actually there to approve/reject a visitor, not an
        // absentee owner. WhatsAppNumber preferred over Phone per Person's own
        // doc comment ("left null means same as mobile for messaging purposes").
        var occupant = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate == null && m.IsPrimary &&
                !m.FlatOccupancy.IsDeleted && m.FlatOccupancy.EndDate == null && m.FlatOccupancy.FlatId == flatId)
            .OrderBy(m => m.FlatOccupancy.Type == OccupancyType.Tenant ? 0 : 1)
            .Select(m => new { m.Person.WhatsAppNumber, m.Person.Phone })
            .FirstOrDefaultAsync(ct);
        var occupantPhone = occupant?.WhatsAppNumber ?? occupant?.Phone;
        if (!string.IsNullOrWhiteSpace(occupantPhone)) return occupantPhone;

        var primaryContactPhone = await _context.FlatResidencies
            .Where(r => !r.IsDeleted && r.MoveOutDate == null && r.IsPrimaryContact && r.FlatId == flatId)
            .Select(r => r.Member.Phone)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(primaryContactPhone)) return primaryContactPhone;

        return await _context.Flats.Where(f => f.Id == flatId).Select(f => f.OwnerPhone).FirstOrDefaultAsync(ct);
    }

    private async Task SendWhatsAppApprovalRequestAsync(int flatId, VisitorVisit visit, VisitorVisitDto dto, CancellationToken ct)
    {
        var ownerPhone = await ResolveFlatOwnerPhoneAsync(flatId, ct);
        if (string.IsNullOrWhiteSpace(ownerPhone))
        {
            _logger.LogWarning(
                "Flat {FlatId} has no resident contact on file (no current primary FlatResidency, no Flat.OwnerPhone) — " +
                "visit {VisitId} created but no WhatsApp approval request sent.",
                flatId, visit.Id);
            return;
        }

        var approvalUrl = _appUrlService.BuildAbsoluteUrl($"api/public/visitor-approvals/{visit.ApprovalToken}");
        var caption =
            $"🔔 New Visitor Request\n\n" +
            $"Visitor: {dto.VisitorName}\n" +
            $"Mobile: {dto.VisitorMobile}\n" +
            $"Purpose: {dto.PurposeName}\n" +
            $"Flat: {dto.FlatNumber}\n" +
            $"Gate: {dto.GateName}\n" +
            $"Visitors: {dto.NumberOfVisitors}\n" +
            $"Requested: {dto.RequestedAt:dd MMM yyyy, hh:mm tt}" +
            (approvalUrl != null ? $"\n\nApprove or reject here:\n{approvalUrl}" : "");

        if (!string.IsNullOrWhiteSpace(dto.VisitorPhotoUrl))
        {
            await _whatsAppService.SendWhatsAppImageAsync(ownerPhone, caption, dto.VisitorPhotoUrl, ct);
        }
        else
        {
            await _whatsAppService.SendWhatsAppAsync(ownerPhone, caption, ct);
        }
    }

    public async Task<Unit> Handle(ApproveVisitCommand request, CancellationToken ct)
    {
        var visit = await GetVisitAsync(request.Id, ct);
        RequirePendingApproval(visit, "approved");
        await EnsureCurrentUserResidesAtAsync(visit.FlatId, ct);
        await ApproveInternalAsync(visit, _currentUserService.UserId, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RejectVisitCommand request, CancellationToken ct)
    {
        var visit = await GetVisitAsync(request.Id, ct);
        RequirePendingApproval(visit, "rejected");
        await EnsureCurrentUserResidesAtAsync(visit.FlatId, ct);
        await RejectInternalAsync(visit, _currentUserService.UserId, request.Reason, ct);
        return Unit.Value;
    }

    /// <summary>No residency check — the WhatsApp link's unguessable token
    /// (validated by GetVisitByTokenAsync) is the only authorization here,
    /// since not every flat has a resident user account to check residency
    /// against in the first place.</summary>
    public async Task<Unit> Handle(ApproveVisitByTokenCommand request, CancellationToken ct)
    {
        var visit = await GetVisitByTokenAsync(request.Token, ct);
        RequirePendingApproval(visit, "approved");
        await ApproveInternalAsync(visit, approvedByUserId: null, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RejectVisitByTokenCommand request, CancellationToken ct)
    {
        var visit = await GetVisitByTokenAsync(request.Token, ct);
        RequirePendingApproval(visit, "rejected");
        await RejectInternalAsync(visit, rejectedByUserId: null, request.Reason, ct);
        return Unit.Value;
    }

    private static void RequirePendingApproval(VisitorVisit visit, string action)
    {
        if (visit.Status != VisitorVisitStatus.PendingApproval)
        {
            throw new ConflictAppException($"Only a pending request can be {action}.");
        }
    }

    private async Task ApproveInternalAsync(VisitorVisit visit, int? approvedByUserId, CancellationToken ct)
    {
        visit.Status = VisitorVisitStatus.Approved;
        visit.ApprovedAt = DateTime.UtcNow;
        visit.ApprovedByUserId = approvedByUserId;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Approve, "Visitors", nameof(VisitorVisit), visit.Id.ToString(), ct: ct);
        await _notificationService.SendToUserAsync(visit.CreatedByUserId, "VisitorApproved", await ProjectAsync(visit.Id, ct), ct);
    }

    private async Task RejectInternalAsync(VisitorVisit visit, int? rejectedByUserId, string? reason, CancellationToken ct)
    {
        visit.Status = VisitorVisitStatus.Rejected;
        visit.RejectedAt = DateTime.UtcNow;
        visit.RejectedByUserId = rejectedByUserId;
        visit.RejectionReason = reason;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Reject, "Visitors", nameof(VisitorVisit), visit.Id.ToString(), ct: ct);
        await _notificationService.SendToUserAsync(visit.CreatedByUserId, "VisitorRejected", await ProjectAsync(visit.Id, ct), ct);
    }

    public async Task<Unit> Handle(CheckInVisitCommand request, CancellationToken ct)
    {
        var visit = await GetVisitAsync(request.Id, ct);
        if (visit.Status != VisitorVisitStatus.Approved)
        {
            throw new ConflictAppException("Only an approved visit can be checked in.");
        }

        visit.Status = VisitorVisitStatus.CheckedIn;
        visit.CheckInTime = DateTime.UtcNow;
        visit.CheckedInByUserId = _currentUserService.UserId;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Visitors", nameof(VisitorVisit), visit.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CheckOutVisitCommand request, CancellationToken ct)
    {
        var visit = await GetVisitAsync(request.Id, ct);
        if (visit.Status != VisitorVisitStatus.CheckedIn)
        {
            throw new ConflictAppException("Only a checked-in visit can be checked out.");
        }

        visit.Status = VisitorVisitStatus.CheckedOut;
        visit.CheckOutTime = DateTime.UtcNow;
        visit.CheckedOutByUserId = _currentUserService.UserId;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Visitors", nameof(VisitorVisit), visit.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CancelVisitCommand request, CancellationToken ct)
    {
        var visit = await GetVisitAsync(request.Id, ct);
        if (visit.Status != VisitorVisitStatus.PendingApproval)
        {
            throw new ConflictAppException("Only a pending request can be cancelled.");
        }

        visit.Status = VisitorVisitStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Visitors", nameof(VisitorVisit), visit.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ExpireVisitRequestCommand request, CancellationToken ct)
    {
        var visit = await GetVisitAsync(request.Id, ct);
        if (visit.Status != VisitorVisitStatus.PendingApproval)
        {
            return Unit.Value;
        }

        visit.Status = VisitorVisitStatus.Expired;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Visitors", nameof(VisitorVisit), visit.Id.ToString(), ct: ct);
        await _notificationService.SendToUserAsync(visit.CreatedByUserId, "VisitorRequestExpired", await ProjectAsync(visit.Id, ct), ct);
        return Unit.Value;
    }

    private async Task<VisitorVisit> GetVisitAsync(int id, CancellationToken ct) =>
        await _context.VisitorVisits.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct)
        ?? throw new NotFoundException(nameof(VisitorVisit), id);

    private async Task<VisitorVisit> GetVisitByTokenAsync(string token, CancellationToken ct) =>
        await _context.VisitorVisits.FirstOrDefaultAsync(v => v.ApprovalToken == token && !v.IsDeleted, ct)
        ?? throw new NotFoundException(nameof(VisitorVisit), token);

    /// <summary>Mirrors EventRsvpFeature's flat-resolution walk, in reverse:
    /// confirms the CURRENT user resides at a SPECIFIC flat, rather than
    /// resolving which flat they reside at. Never trust a client-supplied
    /// FlatId for authorization — this is the actual check.</summary>
    private async Task EnsureCurrentUserResidesAtAsync(int flatId, CancellationToken ct)
    {
        var resides = await _context.IsCurrentResidentOfFlatAsync(_currentUserService.UserId, flatId, ct);

        if (!resides)
        {
            throw new ForbiddenAccessException("You are not a current resident of this flat.");
        }
    }

    private async Task<List<int>> GetCurrentResidentUserIdsAsync(int flatId, CancellationToken ct) =>
        await _context.FlatResidencies
            .Where(r => r.FlatId == flatId && !r.IsDeleted && r.MoveOutDate == null && r.Member.UserId != null)
            .Select(r => r.Member.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);

    private async Task<VisitorVisitDto> ProjectAsync(int id, CancellationToken ct) =>
        await Project(_context.VisitorVisits.Where(v => v.Id == id)).FirstAsync(ct);

    private static IQueryable<VisitorVisitDto> Project(IQueryable<VisitorVisit> query) =>
        query.Select(v => new VisitorVisitDto
        {
            Id = v.Id, VisitorId = v.VisitorId, VisitorName = v.Visitor.Name, VisitorMobile = v.Visitor.MobileNumber,
            VisitorPhotoUrl = v.Visitor.PhotoUrl, VisitorVehicleNumber = v.Visitor.VehicleNumber,
            FlatId = v.FlatId, FlatNumber = v.Flat.FlatNumber, PurposeId = v.PurposeId, PurposeName = v.Purpose.Name,
            GateId = v.GateId, GateName = v.Gate.Name, NumberOfVisitors = v.NumberOfVisitors, Status = v.Status,
            RequestedAt = v.RequestedAt, ApprovedAt = v.ApprovedAt, RejectedAt = v.RejectedAt,
            RejectionReason = v.RejectionReason, CheckInTime = v.CheckInTime, CheckOutTime = v.CheckOutTime
        });
}

// ---- Queries -------------------------------------------------------------------
public record GetVisitsQuery(
    int SocietyId, VisitorVisitStatus? Status, int? GateId, int? FlatId, DateTime? FromDate, DateTime? ToDate,
    string? Search = null, string? SortBy = null, bool SortDescending = true,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<VisitorVisitDto>>;

public record GetPendingApprovalsQuery : IRequest<List<VisitorVisitDto>>;

public record GetMyVisitsQuery(
    DateTime? FromDate = null, DateTime? ToDate = null, string? Search = null,
    string? SortBy = null, bool SortDescending = true,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<VisitorVisitDto>>;

public record GetCurrentlyInsideQuery(int SocietyId) : IRequest<List<VisitorVisitDto>>;

/// <summary>Backs VisitorApprovalPublicController's [AllowAnonymous] page —
/// the token, not a signed-in user, is what scopes this to one visit.</summary>
public record GetVisitByApprovalTokenQuery(string Token) : IRequest<VisitorVisitDto>;

public class VisitorVisitQueryHandlers :
    IRequestHandler<GetVisitsQuery, PaginatedResult<VisitorVisitDto>>,
    IRequestHandler<GetPendingApprovalsQuery, List<VisitorVisitDto>>,
    IRequestHandler<GetMyVisitsQuery, PaginatedResult<VisitorVisitDto>>,
    IRequestHandler<GetCurrentlyInsideQuery, List<VisitorVisitDto>>,
    IRequestHandler<GetVisitByApprovalTokenQuery, VisitorVisitDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public VisitorVisitQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private static IQueryable<VisitorVisitDto> Project(IQueryable<VisitorVisit> query) =>
        query.Select(v => new VisitorVisitDto
        {
            Id = v.Id, VisitorId = v.VisitorId, VisitorName = v.Visitor.Name, VisitorMobile = v.Visitor.MobileNumber,
            VisitorPhotoUrl = v.Visitor.PhotoUrl, VisitorVehicleNumber = v.Visitor.VehicleNumber,
            FlatId = v.FlatId, FlatNumber = v.Flat.FlatNumber, PurposeId = v.PurposeId, PurposeName = v.Purpose.Name,
            GateId = v.GateId, GateName = v.Gate.Name, NumberOfVisitors = v.NumberOfVisitors, Status = v.Status,
            RequestedAt = v.RequestedAt, ApprovedAt = v.ApprovedAt, RejectedAt = v.RejectedAt,
            RejectionReason = v.RejectionReason, CheckInTime = v.CheckInTime, CheckOutTime = v.CheckOutTime
        });

    private static IQueryable<VisitorVisit> ApplySearch(IQueryable<VisitorVisit> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim().ToLower();
        return query.Where(v =>
            v.Visitor.Name.ToLower().Contains(term) ||
            v.Visitor.MobileNumber.ToLower().Contains(term) ||
            v.Flat.FlatNumber.ToLower().Contains(term));
    }

    private static IQueryable<VisitorVisit> ApplySort(IQueryable<VisitorVisit> query, string? sortBy, bool sortDescending) =>
        (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("visitorname", true) => query.OrderByDescending(v => v.Visitor.Name),
            ("visitorname", false) => query.OrderBy(v => v.Visitor.Name),
            ("flatnumber", true) => query.OrderByDescending(v => v.Flat.FlatNumber),
            ("flatnumber", false) => query.OrderBy(v => v.Flat.FlatNumber),
            ("purposename", true) => query.OrderByDescending(v => v.Purpose.Name),
            ("purposename", false) => query.OrderBy(v => v.Purpose.Name),
            ("status", true) => query.OrderByDescending(v => v.Status),
            ("status", false) => query.OrderBy(v => v.Status),
            ("requestedat", false) => query.OrderBy(v => v.RequestedAt),
            _ => query.OrderByDescending(v => v.RequestedAt)
        };

    public async Task<PaginatedResult<VisitorVisitDto>> Handle(GetVisitsQuery request, CancellationToken ct)
    {
        var query = _context.VisitorVisits.Where(v => v.SocietyId == request.SocietyId);

        if (request.Status.HasValue) query = query.Where(v => v.Status == request.Status);
        if (request.GateId.HasValue) query = query.Where(v => v.GateId == request.GateId);
        if (request.FlatId.HasValue) query = query.Where(v => v.FlatId == request.FlatId);
        if (request.FromDate.HasValue) query = query.Where(v => v.RequestedAt >= request.FromDate);
        if (request.ToDate.HasValue) query = query.Where(v => v.RequestedAt <= request.ToDate);
        query = ApplySearch(query, request.Search);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await Project(ApplySort(query, request.SortBy, request.SortDescending))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<VisitorVisitDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<List<VisitorVisitDto>> Handle(GetPendingApprovalsQuery request, CancellationToken ct)
    {
        var flatIds = await _context.GetCurrentResidentFlatIdsAsync(_currentUserService.UserId, ct);

        if (flatIds.Count == 0) return new List<VisitorVisitDto>();

        return await Project(_context.VisitorVisits
                .Where(v => flatIds.Contains(v.FlatId) && v.Status == VisitorVisitStatus.PendingApproval))
            .OrderBy(v => v.RequestedAt)
            .ToListAsync(ct);
    }

    public async Task<PaginatedResult<VisitorVisitDto>> Handle(GetMyVisitsQuery request, CancellationToken ct)
    {
        var flatIds = await _context.GetCurrentResidentFlatIdsAsync(_currentUserService.UserId, ct);
        if (flatIds.Count == 0) return new PaginatedResult<VisitorVisitDto>(new List<VisitorVisitDto>(), 0, request.PageNumber, request.PageSize);

        var query = _context.VisitorVisits.Where(v => flatIds.Contains(v.FlatId));

        if (request.FromDate.HasValue) query = query.Where(v => v.RequestedAt >= request.FromDate);
        if (request.ToDate.HasValue) query = query.Where(v => v.RequestedAt <= request.ToDate);
        query = ApplySearch(query, request.Search);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await Project(ApplySort(query, request.SortBy, request.SortDescending))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<VisitorVisitDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<List<VisitorVisitDto>> Handle(GetCurrentlyInsideQuery request, CancellationToken ct) =>
        await Project(_context.VisitorVisits
                .Where(v => v.SocietyId == request.SocietyId && v.Status == VisitorVisitStatus.CheckedIn))
            .OrderBy(v => v.CheckInTime)
            .ToListAsync(ct);

    public async Task<VisitorVisitDto> Handle(GetVisitByApprovalTokenQuery request, CancellationToken ct) =>
        await Project(_context.VisitorVisits.Where(v => v.ApprovalToken == request.Token)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(VisitorVisit), request.Token);
}
