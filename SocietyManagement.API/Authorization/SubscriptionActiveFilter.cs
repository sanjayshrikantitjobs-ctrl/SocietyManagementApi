using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.API.Authorization;

/// <summary>
/// Global subscription-gating enforcement, modeled directly on
/// SocietyScopeFilter — same registration point (Program.cs), same
/// "no society_id claim = Super Admin, unrestricted" convention. Runs for
/// every request from a regular society user and blocks it with a 402
/// (SubscriptionExpiredException) once that society's subscription window
/// has passed, OR the Super Admin has manually suspended it (see
/// Society.IsSubscriptionSuspended), until the Super Admin extends the
/// window (SetSocietySubscriptionCommand) or lifts the suspension
/// (SetSocietySuspensionCommand).
///
/// Resolved via DI per-request (TypeFilterAttribute, see Program.cs) rather
/// than instantiated directly, since it needs IApplicationDbContext and
/// IMemoryCache — SocietyScopeFilter has no such dependencies so it stays a
/// plain instance.
/// </summary>
public class SubscriptionActiveFilter : IAsyncActionFilter
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public SubscriptionActiveFilter(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    internal static string CacheKey(int societyId) => $"subscription-status:{societyId}";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var societyClaim = user.FindFirst("society_id")?.Value;
        if (string.IsNullOrEmpty(societyClaim) || !int.TryParse(societyClaim, out var callerSocietyId))
        {
            // No claim = Super Admin — unrestricted, same as SocietyScopeFilter.
            await next();
            return;
        }

        var status = await _cache.GetOrCreateAsync(CacheKey(callerSocietyId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _context.Societies
                .Where(s => s.Id == callerSocietyId)
                .Select(s => new SubscriptionStatus(s.SubscriptionEndDate, s.IsSubscriptionSuspended))
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
        });

        if (status.IsSuspended || status.EndDate < DateTime.UtcNow)
        {
            throw new SubscriptionExpiredException();
        }

        await next();
    }

    /// <summary>A struct so FirstOrDefaultAsync's default (no matching
    /// society — shouldn't happen for a valid claim, but defensively) is
    /// EndDate = DateTime.MinValue, IsSuspended = false — still blocked by
    /// the EndDate check above rather than silently passing.</summary>
    private readonly record struct SubscriptionStatus(DateTime EndDate, bool IsSuspended);
}
