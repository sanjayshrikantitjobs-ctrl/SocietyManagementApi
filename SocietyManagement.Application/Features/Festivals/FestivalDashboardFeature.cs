using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalKpisDto
{
    public decimal Budget { get; set; }
    public decimal Collected { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining => Budget - Spent;
    public int SponsorsCount { get; set; }
    public int PendingExpensesCount { get; set; }
    public int VolunteersCount { get; set; }
    public int TasksPendingCount { get; set; }
}

public class BudgetVsActualPointDto
{
    public string CategoryName { get; set; } = default!;
    public decimal Estimated { get; set; }
    public decimal Approved { get; set; }
    public decimal Actual { get; set; }
}

public class ExpenseCategoryPointDto
{
    public string CategoryName { get; set; } = default!;
    public decimal Amount { get; set; }
}

public class SponsorContributionPointDto
{
    public string CompanyName { get; set; } = default!;
    public decimal Promised { get; set; }
    public decimal Received { get; set; }
}

public class FestivalDashboardDto
{
    public FestivalKpisDto Kpis { get; set; } = new();
    public List<BudgetVsActualPointDto> BudgetVsActual { get; set; } = new();
    public List<ExpenseCategoryPointDto> ExpenseByCategory { get; set; } = new();
    public List<SponsorContributionPointDto> SponsorContributions { get; set; } = new();
}

public record GetFestivalDashboardQuery(int FestivalId) : IRequest<FestivalDashboardDto>;

public class FestivalDashboardQueryHandler : IRequestHandler<GetFestivalDashboardQuery, FestivalDashboardDto>
{
    private static readonly ExpenseApprovalStatus[] SpentStatuses = { ExpenseApprovalStatus.Approved, ExpenseApprovalStatus.Paid };

    private readonly IApplicationDbContext _context;

    public FestivalDashboardQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<FestivalDashboardDto> Handle(GetFestivalDashboardQuery request, CancellationToken ct)
    {
        var festivalKind = await _context.Festivals
            .Where(f => f.Id == request.FestivalId && !f.IsDeleted)
            .Select(f => (FestivalKind?)f.Kind)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        var categories = await _context.FestivalBudgetCategories
            .Where(c => c.FestivalId == request.FestivalId && !c.IsDeleted)
            .Select(c => new BudgetVsActualPointDto
            {
                CategoryName = c.Category.ToString(),
                Estimated = c.EstimatedAmount,
                Approved = c.ApprovedAmount,
                Actual = c.Expenses.Where(e => SpentStatuses.Contains(e.ApprovalStatus)).Sum(e => (decimal?)e.Amount) ?? 0
            })
            .OrderBy(c => c.CategoryName)
            .ToListAsync(ct);

        var expenseByCategory = categories
            .Where(c => c.Actual > 0)
            .Select(c => new ExpenseCategoryPointDto { CategoryName = c.CategoryName, Amount = c.Actual })
            .ToList();

        // A Pool festival's own FestivalId rarely has direct sponsors — its
        // Child festivals do (see FestivalKind's doc comment). Additive, not
        // either/or: a Pool could in principle also have direct sponsors.
        var sponsorFestivalIds = new List<int> { request.FestivalId };
        if (festivalKind == FestivalKind.Pool)
        {
            var childIds = await _context.Festivals
                .Where(f => f.ContributionPoolFestivalId == request.FestivalId && !f.IsDeleted)
                .Select(f => f.Id)
                .ToListAsync(ct);
            sponsorFestivalIds.AddRange(childIds);
        }

        var sponsors = await _context.FestivalSponsors
            .Where(s => sponsorFestivalIds.Contains(s.FestivalId) && !s.IsDeleted) // sponsorFestivalIds is a small in-memory list — Contains translates to a SQL IN clause
            .Select(s => new SponsorContributionPointDto
            {
                CompanyName = s.CompanyName,
                Promised = s.PromisedAmount,
                Received = s.ReceivedAmount
            })
            .OrderByDescending(s => s.Promised)
            .ToListAsync(ct);

        var budget = categories.Sum(c => c.Approved);
        var spent = categories.Sum(c => c.Actual);
        var collected = await _context.FestivalContributions
            .Where(c => c.FestivalId == request.FestivalId && !c.IsDeleted)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0;
        var sponsorsCount = sponsors.Count;
        var pendingExpensesCount = await _context.FestivalExpenses
            .CountAsync(e => e.FestivalId == request.FestivalId && !e.IsDeleted && e.ApprovalStatus == ExpenseApprovalStatus.Pending, ct);
        var volunteersCount = await _context.FestivalVolunteers
            .CountAsync(v => v.FestivalId == request.FestivalId && !v.IsDeleted, ct);
        var tasksPendingCount = await _context.FestivalTasks
            .CountAsync(t => t.FestivalId == request.FestivalId && !t.IsDeleted && t.Status != FestivalTaskStatus.Completed, ct);

        return new FestivalDashboardDto
        {
            Kpis = new FestivalKpisDto
            {
                Budget = budget,
                Collected = collected,
                Spent = spent,
                SponsorsCount = sponsorsCount,
                PendingExpensesCount = pendingExpensesCount,
                VolunteersCount = volunteersCount,
                TasksPendingCount = tasksPendingCount
            },
            BudgetVsActual = categories,
            ExpenseByCategory = expenseByCategory,
            SponsorContributions = sponsors
        };
    }
}
