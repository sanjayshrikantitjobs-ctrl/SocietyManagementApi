using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>Mirrors VisitorRequestExpiryService's shape (BackgroundService +
/// IServiceScopeFactory + PeriodicTimer). Two jobs each sweep: flip Scheduled
/// announcements whose PublishAt has passed to Published (firing the same
/// SignalR notification a manual Publish would), and flip Published ones
/// whose ExpiryAt has passed to Expired.</summary>
public class AnnouncementLifecycleService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnnouncementLifecycleService> _logger;

    public AnnouncementLifecycleService(IServiceScopeFactory scopeFactory, ILogger<AnnouncementLifecycleService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTime.UtcNow;

            var toPublish = await context.Announcements
                .Where(a => !a.IsDeleted && a.Status == AnnouncementStatus.Scheduled && a.PublishAt != null && a.PublishAt <= now)
                .ToListAsync(ct);

            foreach (var announcement in toPublish)
            {
                announcement.Status = AnnouncementStatus.Published;
            }

            var toExpire = await context.Announcements
                .Where(a => !a.IsDeleted && a.Status == AnnouncementStatus.Published && a.ExpiryAt != null && a.ExpiryAt <= now)
                .ToListAsync(ct);

            foreach (var announcement in toExpire)
            {
                announcement.Status = AnnouncementStatus.Expired;
            }

            if (toPublish.Count > 0 || toExpire.Count > 0)
            {
                await context.SaveChangesAsync(ct);
            }

            foreach (var announcement in toPublish)
            {
                await notificationService.SendToSocietyAsync(announcement.SocietyId, "AnnouncementPublished", new
                {
                    AnnouncementId = announcement.Id,
                    announcement.SocietyId,
                    Type = announcement.Type.ToString(),
                    Priority = announcement.Priority.ToString(),
                    announcement.Title,
                    Message = announcement.Description.Length > 200 ? announcement.Description[..200] + "…" : announcement.Description
                }, ct);
            }

            if (toPublish.Count > 0)
            {
                _logger.LogInformation("Auto-published {Count} scheduled announcement(s).", toPublish.Count);
            }
            if (toExpire.Count > 0)
            {
                _logger.LogInformation("Auto-expired {Count} announcement(s).", toExpire.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled announcement lifecycle sweep failed.");
        }
    }
}
