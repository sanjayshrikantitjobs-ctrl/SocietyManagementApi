using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Maintenance;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// The first scheduled job in this codebase — no Hangfire/Quartz dependency,
/// just a plain BackgroundService with a PeriodicTimer, matching the
/// solution's otherwise-minimal dependency footprint. Checked hourly (not
/// per-minute) since bill generation only needs day-of-month granularity.
///
/// Registered as a singleton by the hosting model, but MaintenanceSettings/
/// bills live behind the scoped IApplicationDbContext, so every tick opens
/// its own DI scope via IServiceScopeFactory — there is no existing
/// precedent for this in the codebase (grep confirmed zero prior
/// BackgroundService usage), so this is the pattern future scheduled jobs
/// (e.g. Tanker billing) should copy.
/// </summary>
public class MaintenanceBillGenerationService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceBillGenerationService> _logger;

    public MaintenanceBillGenerationService(IServiceScopeFactory scopeFactory, ILogger<MaintenanceBillGenerationService> logger)
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

            var today = DateTime.UtcNow.Day;
            var societyIds = context.MaintenanceSettings
                .Where(s => !s.IsDeleted && s.BillGenerationDay == today)
                .Select(s => s.SocietyId)
                .ToList();

            foreach (var societyId in societyIds)
            {
                var count = await mediator.Send(new GenerateMonthlyBillsCommand(societyId, null), ct);
                if (count > 0)
                {
                    _logger.LogInformation(
                        "Auto-generated {Count} maintenance bill(s) for society {SocietyId}.", count, societyId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled maintenance bill generation failed.");
        }
    }
}
