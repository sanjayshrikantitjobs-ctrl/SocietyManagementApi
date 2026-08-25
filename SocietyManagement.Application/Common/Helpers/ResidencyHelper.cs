using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Application.Common.Helpers;

/// <summary>Centralizes "does this user currently reside at this flat" —
/// there are two parallel resident models (legacy Member/FlatResidency and
/// the newer Person/Occupancy flow; see User.MemberId/User.PersonId doc
/// comments), and checking only one silently 403s every Owner/Tenant login
/// created through the other. This bug shipped independently in
/// GetMyFlatsQuery, GetMyComplaintsQuery, ComplaintFeature's
/// EnsureCurrentUserResidesAtAsync and VisitorVisitFeature's copy of the
/// same method — this helper exists so a fifth copy never repeats it.</summary>
public static class ResidencyHelper
{
    public static async Task<bool> IsCurrentResidentOfFlatAsync(
        this IApplicationDbContext context, int? userId, int flatId, CancellationToken ct)
    {
        var viaMember = await context.Members
            .Where(m => m.UserId == userId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .AnyAsync(r => !r.IsDeleted && r.MoveOutDate == null && r.FlatId == flatId, ct);
        if (viaMember) return true;

        return await context.Users
            .Where(u => u.Id == userId && u.PersonId != null)
            .SelectMany(u => context.OccupancyMembers
                .Where(om => om.PersonId == u.PersonId && !om.IsDeleted && om.LeftDate == null))
            .AnyAsync(om => !om.FlatOccupancy.IsDeleted && om.FlatOccupancy.EndDate == null && om.FlatOccupancy.FlatId == flatId, ct);
    }
}
