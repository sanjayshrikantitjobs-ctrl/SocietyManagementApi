using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Festivals;

public class ContributionPoolDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public int Year { get; set; }
}

public class PoolChildSummaryDto
{
    public int FestivalId { get; set; }
    public string Name { get; set; } = default!;
    public FestivalStatus Status { get; set; }
    public decimal Budget { get; set; }
    public decimal Spent { get; set; }
}

/// <summary>A Pool festival's own dashboard data: how much it collected,
/// how much every Child festival linked to it has spent, and what's left.</summary>
public class PoolSummaryDto
{
    public int FestivalId { get; set; }
    public decimal PoolCollected { get; set; }
    public decimal ChildrenSpent { get; set; }
    public decimal PoolRemaining => PoolCollected - ChildrenSpent;
    public List<PoolChildSummaryDto> Children { get; set; } = new();
}

/// <summary>For a Child festival's own dashboard — its linked pool's name
/// and how much of that shared pool remains after every child's spending.</summary>
public class ChildPoolStatusDto
{
    public int PoolFestivalId { get; set; }
    public string PoolFestivalName { get; set; } = default!;
    public decimal PoolRemaining { get; set; }
}

public record GetContributionPoolsQuery(int SocietyId) : IRequest<List<ContributionPoolDto>>;

public record GetPoolSummaryQuery(int FestivalId) : IRequest<PoolSummaryDto>;

public record GetChildPoolStatusQuery(int FestivalId) : IRequest<ChildPoolStatusDto?>;

public class FestivalContributionPoolQueryHandlers :
    IRequestHandler<GetContributionPoolsQuery, List<ContributionPoolDto>>,
    IRequestHandler<GetPoolSummaryQuery, PoolSummaryDto>,
    IRequestHandler<GetChildPoolStatusQuery, ChildPoolStatusDto?>
{
    private static readonly ExpenseApprovalStatus[] SpentStatuses = { ExpenseApprovalStatus.Approved, ExpenseApprovalStatus.Paid };

    private readonly IApplicationDbContext _context;

    public FestivalContributionPoolQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<ContributionPoolDto>> Handle(GetContributionPoolsQuery request, CancellationToken ct) =>
        await _context.Festivals
            .Where(f => f.SocietyId == request.SocietyId && !f.IsDeleted && f.Kind == FestivalKind.Pool)
            .OrderByDescending(f => f.Year).ThenBy(f => f.Name)
            .Select(f => new ContributionPoolDto { Id = f.Id, Name = f.Name, Year = f.Year })
            .ToListAsync(ct);

    public async Task<PoolSummaryDto> Handle(GetPoolSummaryQuery request, CancellationToken ct)
    {
        var pool = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        if (pool.Kind != FestivalKind.Pool)
        {
            throw new ConflictAppException("This festival is not a Contribution Pool.");
        }

        var poolCollected = await _context.FestivalContributions
            .Where(c => c.FestivalId == pool.Id && !c.IsDeleted)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0;

        var children = await _context.Festivals
            .Where(f => f.ContributionPoolFestivalId == pool.Id && !f.IsDeleted)
            .Select(f => new PoolChildSummaryDto
            {
                FestivalId = f.Id,
                Name = f.Name,
                Status = f.Status,
                Budget = f.BudgetCategories.Sum(c => (decimal?)c.ApprovedAmount) ?? 0,
                Spent = f.Expenses.Where(e => SpentStatuses.Contains(e.ApprovalStatus)).Sum(e => (decimal?)e.Amount) ?? 0
            })
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return new PoolSummaryDto
        {
            FestivalId = pool.Id,
            PoolCollected = poolCollected,
            ChildrenSpent = children.Sum(c => c.Spent),
            Children = children
        };
    }

    public async Task<ChildPoolStatusDto?> Handle(GetChildPoolStatusQuery request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        if (festival.Kind != FestivalKind.Child || festival.ContributionPoolFestivalId is null)
        {
            return null;
        }

        var summary = await Handle(new GetPoolSummaryQuery(festival.ContributionPoolFestivalId.Value), ct);
        var poolName = await _context.Festivals
            .Where(f => f.Id == festival.ContributionPoolFestivalId)
            .Select(f => f.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return new ChildPoolStatusDto
        {
            PoolFestivalId = summary.FestivalId, PoolFestivalName = poolName, PoolRemaining = summary.PoolRemaining
        };
    }
}
