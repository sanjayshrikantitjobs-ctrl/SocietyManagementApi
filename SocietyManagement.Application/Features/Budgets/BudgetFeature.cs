using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Budgets;

public class BudgetLineDto
{
    public ExpenseCategory Category { get; set; }
    public int? BudgetId { get; set; }
    public decimal Budgeted { get; set; }
    public decimal Actual { get; set; }
    public decimal Remaining => Budgeted - Actual;
}

public class BudgetOverviewDto
{
    public int FinancialYear { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalBudgeted { get; set; }
    public decimal TotalActual { get; set; }
    public List<BudgetLineDto> Lines { get; set; } = new();
}

// Financial year runs April to March and is identified by its starting year
// (2026 = 1 Apr 2026 - 31 Mar 2027).
public record GetBudgetOverviewQuery(int SocietyId, int FinancialYear) : IRequest<BudgetOverviewDto>;

// Upsert: one budget per (society, year, category).
public record SetBudgetCommand(int SocietyId, int FinancialYear, ExpenseCategory Category, decimal Amount) : IRequest<Unit>;

public class SetBudgetCommandValidator : AbstractValidator<SetBudgetCommand>
{
    public SetBudgetCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.FinancialYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}

public class BudgetHandlers :
    IRequestHandler<GetBudgetOverviewQuery, BudgetOverviewDto>,
    IRequestHandler<SetBudgetCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public BudgetHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<BudgetOverviewDto> Handle(GetBudgetOverviewQuery r, CancellationToken ct)
    {
        var start = new DateTime(r.FinancialYear, 4, 1);
        var end = start.AddYears(1);

        var budgets = await _context.Budgets.Where(b => b.SocietyId == r.SocietyId && b.FinancialYear == r.FinancialYear).ToListAsync(ct);
        var actuals = await _context.Expenses
            .Where(e => e.SocietyId == r.SocietyId && e.ExpenseDate >= start && e.ExpenseDate < end)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var lines = Enum.GetValues<ExpenseCategory>().Select(c =>
        {
            var b = budgets.FirstOrDefault(x => x.Category == c);
            return new BudgetLineDto
            {
                Category = c, BudgetId = b?.Id, Budgeted = b?.Amount ?? 0m, Actual = actuals.FirstOrDefault(a => a.Category == c)?.Total ?? 0m
            };
        }).ToList();

        return new BudgetOverviewDto
        {
            FinancialYear = r.FinancialYear, PeriodStart = start, PeriodEnd = end.AddDays(-1),
            TotalBudgeted = lines.Sum(l => l.Budgeted), TotalActual = lines.Sum(l => l.Actual), Lines = lines
        };
    }

    public async Task<Unit> Handle(SetBudgetCommand r, CancellationToken ct)
    {
        var existing = await _context.Budgets.FirstOrDefaultAsync(
            b => b.SocietyId == r.SocietyId && b.FinancialYear == r.FinancialYear && b.Category == r.Category, ct);
        if (existing is null)
        {
            await _context.Budgets.AddAsync(new Budget { SocietyId = r.SocietyId, FinancialYear = r.FinancialYear, Category = r.Category, Amount = r.Amount }, ct);
        }
        else
        {
            existing.Amount = r.Amount;
        }
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Finance", nameof(Budget), $"{r.SocietyId}-{r.FinancialYear}-{r.Category}",
            newValues: new { r.Amount }, ct: ct);
        return Unit.Value;
    }
}
