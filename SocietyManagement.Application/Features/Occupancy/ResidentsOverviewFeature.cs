using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Occupancy;

/// <summary>The Residents "Overview" tab's stat-card row. Owner/Tenant
/// Occupied and Vacant are computed live from FlatOccupancy (EndDate ==
/// null rows) rather than the older, manually-set Flat.Status — that field
/// isn't kept in sync with this module and would give inconsistent numbers
/// next to the Owners/Tenants tabs.</summary>
public class ResidentsOverviewSummaryDto
{
    public int TotalFlats { get; set; }
    public int OwnerOccupiedFlats { get; set; }
    public int TenantOccupiedFlats { get; set; }
    public int VacantFlats { get; set; }
    public int TotalMembers { get; set; }
    public int TotalOwners { get; set; }
    public int TotalTenants { get; set; }
}

public class RecentOccupancyChangeDto
{
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public OccupancyType Type { get; set; }
    public bool MovedIn { get; set; }
    public string PersonName { get; set; } = default!;
    public DateTime ChangeDate { get; set; }
}

public record GetResidentsOverviewSummaryQuery(int SocietyId) : IRequest<ResidentsOverviewSummaryDto>;

public record GetRecentOccupancyChangesQuery(int SocietyId, int Take = 10) : IRequest<List<RecentOccupancyChangeDto>>;

public class ResidentsOverviewQueryHandlers :
    IRequestHandler<GetResidentsOverviewSummaryQuery, ResidentsOverviewSummaryDto>,
    IRequestHandler<GetRecentOccupancyChangesQuery, List<RecentOccupancyChangeDto>>
{
    private readonly IApplicationDbContext _context;

    public ResidentsOverviewQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<ResidentsOverviewSummaryDto> Handle(GetResidentsOverviewSummaryQuery request, CancellationToken ct)
    {
        var totalFlats = await _context.Flats
            .CountAsync(f => !f.IsDeleted && f.Floor.Wing.Building.SocietyId == request.SocietyId, ct);

        var ownerOccupiedFlatIds = await _context.FlatOccupancies
            .Where(o => !o.IsDeleted && o.EndDate == null && o.Type == OccupancyType.Owner
                && o.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(o => o.FlatId)
            .Distinct()
            .ToListAsync(ct);

        var tenantOccupiedFlatIds = await _context.FlatOccupancies
            .Where(o => !o.IsDeleted && o.EndDate == null && o.Type == OccupancyType.Tenant
                && o.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(o => o.FlatId)
            .Distinct()
            .ToListAsync(ct);

        var occupiedFlatCount = ownerOccupiedFlatIds.Union(tenantOccupiedFlatIds).Count();

        var totalOwners = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate == null && m.FlatOccupancy.Type == OccupancyType.Owner
                && m.FlatOccupancy.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(m => m.PersonId)
            .Distinct()
            .CountAsync(ct);

        var totalTenants = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate == null && m.FlatOccupancy.Type == OccupancyType.Tenant
                && m.FlatOccupancy.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(m => m.PersonId)
            .Distinct()
            .CountAsync(ct);

        var totalMembers = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate == null
                && m.FlatOccupancy.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(m => m.PersonId)
            .Distinct()
            .CountAsync(ct);

        return new ResidentsOverviewSummaryDto
        {
            TotalFlats = totalFlats,
            OwnerOccupiedFlats = ownerOccupiedFlatIds.Count,
            TenantOccupiedFlats = tenantOccupiedFlatIds.Count,
            VacantFlats = totalFlats - occupiedFlatCount,
            TotalMembers = totalMembers,
            TotalOwners = totalOwners,
            TotalTenants = totalTenants
        };
    }

    public async Task<List<RecentOccupancyChangeDto>> Handle(GetRecentOccupancyChangesQuery request, CancellationToken ct)
    {
        var recentJoins = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.FlatOccupancy.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(request.Take)
            .Select(m => new RecentOccupancyChangeDto
            {
                FlatId = m.FlatOccupancy.FlatId, FlatNumber = m.FlatOccupancy.Flat.FlatNumber, Type = m.FlatOccupancy.Type,
                MovedIn = true, PersonName = m.Person.FirstName + " " + m.Person.LastName, ChangeDate = m.CreatedAt
            })
            .ToListAsync(ct);

        var recentDepartures = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate != null && m.FlatOccupancy.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .OrderByDescending(m => m.ModifiedAt)
            .Take(request.Take)
            .Select(m => new RecentOccupancyChangeDto
            {
                FlatId = m.FlatOccupancy.FlatId, FlatNumber = m.FlatOccupancy.Flat.FlatNumber, Type = m.FlatOccupancy.Type,
                MovedIn = false, PersonName = m.Person.FirstName + " " + m.Person.LastName,
                ChangeDate = m.ModifiedAt ?? m.LeftDate!.Value
            })
            .ToListAsync(ct);

        return recentJoins.Concat(recentDepartures)
            .OrderByDescending(c => c.ChangeDate)
            .Take(request.Take)
            .ToList();
    }
}
