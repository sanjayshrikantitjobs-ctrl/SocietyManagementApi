using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Visitors;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>Mirrors MaintenanceBillGenerationService's shape exactly (same
/// BackgroundService + IServiceScopeFactory + PeriodicTimer pattern), just
/// checked every minute instead of hourly since a pending visitor approval
/// needs to expire on the order of minutes, not days.</summary>
public class VisitorRequestExpiryService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitorRequestExpiryService> _logger;

    public VisitorRequestExpiryService(IServiceScopeFactory scopeFactory, ILogger<VisitorRequestExpiryService> logger)
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
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            var now = DateTime.UtcNow;

            // Left-join semantics done in memory rather than SQL: a society
            // that has never opened the Visitor Settings screen has no
            // VisitorSettings row yet (it's created lazily on first read by
            // GetVisitorSettingsQuery), and an inner join would silently
            // skip its pending requests forever. Default to 30 minutes here
            // instead, matching VisitorSettings.ApprovalRequestExpiryMinutes's
            // own default.
            var expiryMinutesBySociety = await context.VisitorSettings
                .Where(s => !s.IsDeleted)
                .ToDictionaryAsync(s => s.SocietyId, s => s.ApprovalRequestExpiryMinutes, ct);

            var pending = await context.VisitorVisits
                .Where(v => !v.IsDeleted && v.Status == VisitorVisitStatus.PendingApproval)
                .Select(v => new { v.Id, v.SocietyId, v.RequestedAt })
                .ToListAsync(ct);

            var expiredIds = pending
                .Where(v => v.RequestedAt.AddMinutes(expiryMinutesBySociety.GetValueOrDefault(v.SocietyId, 30)) < now)
                .Select(v => v.Id)
                .ToList();

            foreach (var id in expiredIds)
            {
                await mediator.Send(new ExpireVisitRequestCommand(id), ct);
            }

            if (expiredIds.Count > 0)
            {
                _logger.LogInformation("Expired {Count} pending visitor approval request(s).", expiredIds.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled visitor request expiry failed.");
        }
    }
}
