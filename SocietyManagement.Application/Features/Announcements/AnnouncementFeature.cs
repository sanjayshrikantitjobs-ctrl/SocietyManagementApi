using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Announcements;

public class AnnouncementDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public AnnouncementType Type { get; set; }
    public AnnouncementPriority Priority { get; set; }
    public string? AttachmentUrl { get; set; }
    public DateTime? PublishAt { get; set; }
    public DateTime? ExpiryAt { get; set; }
    public AnnouncementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Only meaningful for the caller it was resolved for — set by
    /// GetPublishedAnnouncementsQuery/GetAnnouncementByIdQuery from the
    /// current user's own AnnouncementRead rows, always false on the admin
    /// list (which isn't per-user).</summary>
    public bool IsRead { get; set; }
}

// ==================== Commands ====================

/// <summary>PublishNow bypasses PublishAt entirely (Status goes straight to
/// Published and the notification fires immediately); otherwise a future
/// PublishAt schedules it (AnnouncementLifecycleService flips it later), and
/// no PublishAt at all leaves it a Draft.</summary>
public record CreateAnnouncementCommand(
    int SocietyId, string Title, string Description, AnnouncementType Type, AnnouncementPriority Priority,
    string? AttachmentUrl, DateTime? PublishAt, DateTime? ExpiryAt, bool PublishNow) : IRequest<int>;

public class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ExpiryAt).GreaterThan(x => x.PublishAt)
            .When(x => x.PublishAt.HasValue && x.ExpiryAt.HasValue)
            .WithMessage("Expiry must be after the publish time.");
    }
}

public record UpdateAnnouncementCommand(
    int Id, string Title, string Description, AnnouncementType Type, AnnouncementPriority Priority,
    string? AttachmentUrl, DateTime? PublishAt, DateTime? ExpiryAt) : IRequest;

public class UpdateAnnouncementCommandValidator : AbstractValidator<UpdateAnnouncementCommand>
{
    public UpdateAnnouncementCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ExpiryAt).GreaterThan(x => x.PublishAt)
            .When(x => x.PublishAt.HasValue && x.ExpiryAt.HasValue)
            .WithMessage("Expiry must be after the publish time.");
    }
}

public record DeleteAnnouncementCommand(int Id) : IRequest;

/// <summary>Manually publish a Draft/Scheduled announcement right now,
/// regardless of whatever PublishAt was set to.</summary>
public record PublishAnnouncementCommand(int Id) : IRequest;

/// <summary>Idempotent — marking an already-read announcement read again is
/// a no-op, not an error, since the UI fires this on every detail-view open.</summary>
public record MarkAnnouncementReadCommand(int Id) : IRequest;

// ==================== Queries ====================

public record GetAnnouncementsQuery(
    int SocietyId, AnnouncementStatus? Status, AnnouncementType? Type, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<AnnouncementDto>>;

/// <summary>Resident feed — Published only, and only while not yet expired
/// (ExpiryAt null or in the future); IsRead resolved for the calling user.</summary>
public record GetPublishedAnnouncementsQuery(int SocietyId, int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize)
    : IRequest<PaginatedResult<AnnouncementDto>>;

public record GetAnnouncementByIdQuery(int Id) : IRequest<AnnouncementDto>;

public record GetUnreadAnnouncementCountQuery(int SocietyId) : IRequest<int>;

// ==================== Handlers ====================

public class AnnouncementCommandHandlers :
    IRequestHandler<CreateAnnouncementCommand, int>,
    IRequestHandler<UpdateAnnouncementCommand>,
    IRequestHandler<DeleteAnnouncementCommand>,
    IRequestHandler<PublishAnnouncementCommand>,
    IRequestHandler<MarkAnnouncementReadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;
    private readonly IAuditService _auditService;

    public AnnouncementCommandHandlers(
        IApplicationDbContext context, ICurrentUserService currentUser,
        INotificationService notificationService, IAuditService auditService)
    {
        _context = context;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _auditService = auditService;
    }

    /// <summary>SocietyScopeFilter already rejected a mismatched caller
    /// before this handler ever runs (Announcement.SocietyId is a bound
    /// command/query property it recognizes) — this only covers the by-Id
    /// actions the filter's own doc comment says it can't see.</summary>
    private async Task<Announcement> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Announcement), id);

        if (_currentUser.SocietyId is { } callerSocietyId && announcement.SocietyId != callerSocietyId)
        {
            throw new ForbiddenAccessException("You do not have access to this society's data.");
        }

        return announcement;
    }

    private async Task PublishNowAsync(Announcement announcement, CancellationToken ct)
    {
        announcement.Status = AnnouncementStatus.Published;
        announcement.PublishAt ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _notificationService.SendToSocietyAsync(announcement.SocietyId, "AnnouncementPublished", new
        {
            AnnouncementId = announcement.Id,
            announcement.SocietyId,
            Type = announcement.Type.ToString(),
            Priority = announcement.Priority.ToString(),
            announcement.Title,
            Message = announcement.Description.Length > 200 ? announcement.Description[..200] + "…" : announcement.Description
        }, ct);
    }

    public async Task<int> Handle(CreateAnnouncementCommand request, CancellationToken ct)
    {
        var status = request.PublishNow
            ? AnnouncementStatus.Published
            : request.PublishAt.HasValue
                ? AnnouncementStatus.Scheduled
                : AnnouncementStatus.Draft;

        var announcement = new Announcement
        {
            SocietyId = request.SocietyId, Title = request.Title, Description = request.Description,
            Type = request.Type, Priority = request.Priority, AttachmentUrl = request.AttachmentUrl,
            PublishAt = request.PublishNow ? DateTime.UtcNow : request.PublishAt, ExpiryAt = request.ExpiryAt,
            Status = status
        };

        await _context.Announcements.AddAsync(announcement, ct);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync(AuditAction.Create, "Announcements", nameof(Announcement), announcement.Id.ToString(),
            newValues: new { announcement.Title, announcement.Type, announcement.Status }, ct: ct);

        if (request.PublishNow)
        {
            await _notificationService.SendToSocietyAsync(announcement.SocietyId, "AnnouncementPublished", new
            {
                AnnouncementId = announcement.Id,
                announcement.SocietyId,
                Type = announcement.Type.ToString(),
                Priority = announcement.Priority.ToString(),
                announcement.Title,
                Message = announcement.Description.Length > 200 ? announcement.Description[..200] + "…" : announcement.Description
            }, ct);
        }

        return announcement.Id;
    }

    public async Task Handle(UpdateAnnouncementCommand request, CancellationToken ct)
    {
        var announcement = await LoadOwnedAsync(request.Id, ct);

        announcement.Title = request.Title;
        announcement.Description = request.Description;
        announcement.Type = request.Type;
        announcement.Priority = request.Priority;
        announcement.AttachmentUrl = request.AttachmentUrl;
        announcement.ExpiryAt = request.ExpiryAt;

        // Only re-derive the schedule while it hasn't gone live yet — editing
        // a Published/Expired announcement's content shouldn't quietly
        // re-schedule or re-notify it.
        if (announcement.Status is AnnouncementStatus.Draft or AnnouncementStatus.Scheduled)
        {
            announcement.PublishAt = request.PublishAt;
            announcement.Status = request.PublishAt.HasValue ? AnnouncementStatus.Scheduled : AnnouncementStatus.Draft;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Announcements", nameof(Announcement), announcement.Id.ToString(),
            newValues: new { announcement.Title, announcement.Status }, ct: ct);
    }

    public async Task Handle(DeleteAnnouncementCommand request, CancellationToken ct)
    {
        var announcement = await LoadOwnedAsync(request.Id, ct);
        announcement.IsDeleted = true;
        announcement.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Announcements", nameof(Announcement), announcement.Id.ToString(), ct: ct);
    }

    public async Task Handle(PublishAnnouncementCommand request, CancellationToken ct)
    {
        var announcement = await LoadOwnedAsync(request.Id, ct);
        if (announcement.Status is AnnouncementStatus.Published or AnnouncementStatus.Expired) return;

        await PublishNowAsync(announcement, ct);
        await _auditService.LogAsync(AuditAction.Update, "Announcements", nameof(Announcement), announcement.Id.ToString(),
            newValues: new { Status = "Published" }, ct: ct);
    }

    public async Task Handle(MarkAnnouncementReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var alreadyRead = await _context.AnnouncementReads
            .AnyAsync(r => r.AnnouncementId == request.Id && r.UserId == userId, ct);
        if (alreadyRead) return;

        // A resident marking something read they can't even see is harmless
        // (no data leaks either way), but still worth the same ownership
        // check other by-Id actions get.
        await LoadOwnedAsync(request.Id, ct);

        await _context.AnnouncementReads.AddAsync(
            new AnnouncementRead { AnnouncementId = request.Id, UserId = userId, ReadAt = DateTime.UtcNow }, ct);
        await _context.SaveChangesAsync(ct);
    }
}

public class AnnouncementQueryHandlers :
    IRequestHandler<GetAnnouncementsQuery, PaginatedResult<AnnouncementDto>>,
    IRequestHandler<GetPublishedAnnouncementsQuery, PaginatedResult<AnnouncementDto>>,
    IRequestHandler<GetAnnouncementByIdQuery, AnnouncementDto>,
    IRequestHandler<GetUnreadAnnouncementCountQuery, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AnnouncementQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private static AnnouncementDto Project(Announcement a) => new()
    {
        Id = a.Id, SocietyId = a.SocietyId, Title = a.Title, Description = a.Description, Type = a.Type,
        Priority = a.Priority, AttachmentUrl = a.AttachmentUrl, PublishAt = a.PublishAt, ExpiryAt = a.ExpiryAt,
        Status = a.Status, CreatedAt = a.CreatedAt
    };

    public async Task<PaginatedResult<AnnouncementDto>> Handle(GetAnnouncementsQuery request, CancellationToken ct)
    {
        var query = _context.Announcements.Where(a => a.SocietyId == request.SocietyId);

        if (request.Status.HasValue) query = query.Where(a => a.Status == request.Status.Value);
        if (request.Type.HasValue) query = query.Where(a => a.Type == request.Type.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query.OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(a => Project(a)).ToListAsync(ct);

        return new PaginatedResult<AnnouncementDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PaginatedResult<AnnouncementDto>> Handle(GetPublishedAnnouncementsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = _context.Announcements.Where(a =>
            a.SocietyId == request.SocietyId && a.Status == AnnouncementStatus.Published &&
            (a.ExpiryAt == null || a.ExpiryAt > now));

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query.OrderByDescending(a => a.PublishAt ?? a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var userId = _currentUser.UserId;
        var readIds = userId is null
            ? new HashSet<int>()
            : (await _context.AnnouncementReads
                .Where(r => r.UserId == userId.Value && items.Select(a => a.Id).Contains(r.AnnouncementId))
                .Select(r => r.AnnouncementId).ToListAsync(ct)).ToHashSet();

        var dtos = items.Select(a =>
        {
            var dto = Project(a);
            dto.IsRead = readIds.Contains(a.Id);
            return dto;
        }).ToList();

        return new PaginatedResult<AnnouncementDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<AnnouncementDto> Handle(GetAnnouncementByIdQuery request, CancellationToken ct)
    {
        var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Announcement), request.Id);

        if (_currentUser.SocietyId is { } callerSocietyId && announcement.SocietyId != callerSocietyId)
        {
            throw new ForbiddenAccessException("You do not have access to this society's data.");
        }

        var dto = Project(announcement);
        if (_currentUser.UserId is { } userId)
        {
            dto.IsRead = await _context.AnnouncementReads.AnyAsync(r => r.AnnouncementId == request.Id && r.UserId == userId, ct);
        }

        return dto;
    }

    public async Task<int> Handle(GetUnreadAnnouncementCountQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return 0;

        var now = DateTime.UtcNow;
        var readIds = _context.AnnouncementReads.Where(r => r.UserId == userId.Value).Select(r => r.AnnouncementId);

        return await _context.Announcements
            .Where(a => a.SocietyId == request.SocietyId && a.Status == AnnouncementStatus.Published &&
                        (a.ExpiryAt == null || a.ExpiryAt > now) && !readIds.Contains(a.Id))
            .CountAsync(ct);
    }
}
