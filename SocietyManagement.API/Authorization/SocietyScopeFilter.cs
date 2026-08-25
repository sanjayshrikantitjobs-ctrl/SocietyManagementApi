using System.Reflection;
using Microsoft.AspNetCore.Mvc.Filters;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.API.Authorization;

/// <summary>
/// Centralized multi-tenant enforcement. Every module in this app follows
/// the same convention — a `[FromQuery] int societyId` primitive parameter,
/// or a bound command/query record with a `SocietyId` property — so instead
/// of hand-editing every handler to check "does this societyId belong to
/// the caller," one global filter inspects every action's bound arguments
/// for that convention and rejects the request if it doesn't match the
/// caller's own `society_id` JWT claim. A caller with no such claim is a
/// Super Admin (see JwtService.GenerateAccessToken) and is never restricted.
///
/// This does NOT cover actions that mutate an existing entity purely by its
/// own `Id` (no SocietyId argument at all for this filter to see, e.g.
/// "assign this complaint") — those need a per-handler check after the
/// entity is loaded. Only SocietiesController's own Update/Delete have that
/// check added so far; see the Super Admin plan for the rest as follow-up.
/// </summary>
public class SocietyScopeFilter : IAsyncActionFilter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo?> SocietyIdPropertyCache = new();

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
            // No claim = Super Admin — unrestricted.
            await next();
            return;
        }

        foreach (var (name, value) in context.ActionArguments)
        {
            if (value is null) continue;

            if (value is int primitiveSocietyId)
            {
                // Every controller in this codebase also takes plain `int
                // id` route parameters (GetById, Update, Delete, Assign...)
                // that are NOT a societyId — only compare when the
                // parameter is actually named societyId; anything else
                // (an entity's own id) is intentionally left to that
                // handler's own post-load ownership check.
                if (!string.Equals(name, "societyId", StringComparison.OrdinalIgnoreCase)) continue;
                if (primitiveSocietyId != callerSocietyId)
                {
                    throw new ForbiddenAccessException("You do not have access to this society's data.");
                }
                continue;
            }

            var type = value.GetType();
            var societyIdProperty = SocietyIdPropertyCache.GetOrAdd(type, t => t.GetProperty("SocietyId"));
            if (societyIdProperty?.PropertyType == typeof(int) && societyIdProperty.GetValue(value) is int boundSocietyId)
            {
                if (boundSocietyId != callerSocietyId)
                {
                    throw new ForbiddenAccessException("You do not have access to this society's data.");
                }
            }
        }

        await next();
    }
}
