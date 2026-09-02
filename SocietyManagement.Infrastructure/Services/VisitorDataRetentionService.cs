using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>Mirrors MaintenanceBillGenerationService's BackgroundService +
/// IServiceScopeFactory + PeriodicTimer shape. Checked daily — retention is
/// day-granularity, no need for anything tighter.
///
/// Hard-deletes (not the usual IsDeleted soft-delete) — a retention policy
/// exists specifically so this data stops existing, in the database and in
/// blob/disk storage, past the configured window; a soft-deleted row would
/// still occupy both.
///
/// A VisitorVisit older than the window is deleted outright. A Visitor
/// (the reusable person record — see its own doc comment) is only deleted
/// once none of its visits are left, so a repeat visitor's photo/record
/// survives as long as any one of their visits is still within the
/// window.</summary>
public class VisitorDataRetentionService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private const int DefaultRetentionDays = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitorDataRetentionService> _logger;

    public VisitorDataRetentionService(IServiceScopeFactory scopeFactory, ILogger<VisitorDataRetentionService> logger)
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
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            var now = DateTime.UtcNow;

            // Left-join semantics in memory, same reasoning as
            // VisitorRequestExpiryService: a society with no VisitorSettings
            // row yet still gets the documented default, not "never expires".
            var retentionDaysBySociety = await context.VisitorSettings
                .Where(s => !s.IsDeleted)
                .ToDictionaryAsync(s => s.SocietyId, s => s.RetentionDays, ct);

            var societyIds = await context.Societies.Where(s => !s.IsDeleted).Select(s => s.Id).ToListAsync(ct);

            var deletedVisits = 0;
            var deletedVisitors = 0;

            foreach (var societyId in societyIds)
            {
                var retentionDays = retentionDaysBySociety.GetValueOrDefault(societyId, DefaultRetentionDays);
                var cutoff = now.AddDays(-retentionDays);

                var oldVisits = await context.VisitorVisits
                    .Where(v => v.SocietyId == societyId && v.RequestedAt < cutoff)
                    .ToListAsync(ct);
                if (oldVisits.Count > 0)
                {
                    context.VisitorVisits.RemoveRange(oldVisits);
                    await context.SaveChangesAsync(ct);
                    deletedVisits += oldVisits.Count;
                }

                var orphanedVisitors = await context.Visitors
                    .Where(v => v.SocietyId == societyId && !v.Visits.Any())
                    .ToListAsync(ct);
                foreach (var visitor in orphanedVisitors)
                {
                    if (!string.IsNullOrWhiteSpace(visitor.PhotoUrl))
                    {
                        await fileStorage.DeleteAsync(visitor.PhotoUrl, ct);
                    }
                }
                if (orphanedVisitors.Count > 0)
                {
                    context.Visitors.RemoveRange(orphanedVisitors);
                    await context.SaveChangesAsync(ct);
                    deletedVisitors += orphanedVisitors.Count;
                }
            }

            if (deletedVisits > 0 || deletedVisitors > 0)
            {
                _logger.LogInformation(
                    "Visitor data retention: deleted {VisitCount} visit(s) and {VisitorCount} visitor record(s) (with photos) past their configured retention window.",
                    deletedVisits, deletedVisitors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled visitor data retention cleanup failed.");
        }
    }
}
