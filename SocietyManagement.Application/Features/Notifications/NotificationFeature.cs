using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Notifications;

public class NotificationDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}

// ==================== Queries ====================

/// <summary>Always implicitly scoped to the calling user via
/// ICurrentUserService — there is no societyId/userId parameter to pass in,
/// by design, so a caller can never even attempt to ask for someone else's
/// notifications.</summary>
public record GetMyNotificationsQuery(bool UnreadOnly = false, int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize)
    : IRequest<PaginatedResult<NotificationDto>>;

public record GetUnreadNotificationCountQuery : IRequest<int>;

public record MarkNotificationReadCommand(int Id) : IRequest;

public record MarkAllNotificationsReadCommand : IRequest;

// ==================== Handlers ====================

public class NotificationQueryHandlers :
    IRequestHandler<GetMyNotificationsQuery, PaginatedResult<NotificationDto>>,
    IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public NotificationQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var query = _context.Notifications.Where(n => n.UserId == userId);
        if (request.UnreadOnly) query = query.Where(n => !n.IsRead);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id, EventType = n.EventType, Title = n.Title, Message = n.Message,
                PayloadJson = n.PayloadJson, CreatedAt = n.CreatedAt, IsRead = n.IsRead, ReadAt = n.ReadAt
            })
            .ToListAsync(ct);

        return new PaginatedResult<NotificationDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        return await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }
}

public class NotificationCommandHandlers :
    IRequestHandler<MarkNotificationReadCommand>,
    IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public NotificationCommandHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        // Scoped to (Id, UserId) in one query rather than "load by Id, then
        // check ownership" — a mismatched UserId falls straight through to
        // NotFoundException instead of ever distinguishing "doesn't exist"
        // from "exists but isn't yours" to the caller.
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Notification), request.Id);

        if (notification.IsRead) return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var now = DateTime.UtcNow;

        var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        if (unread.Count > 0) await _context.SaveChangesAsync(ct);
    }
}
