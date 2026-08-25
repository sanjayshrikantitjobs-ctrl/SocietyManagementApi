using MediatR;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Application.Features.Finance;

public class FinanceCategoryAmountDto
{
    public string Label { get; set; } = default!;
    public decimal Amount { get; set; }
}

public class FinanceMonthPointDto
{
    public string MonthLabel { get; set; } = default!;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
}

public class FinanceTransactionDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = default!; // "Income" | "Expense"
    public string Source { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }
}

public class FinanceOverviewDto
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal PendingCollection { get; set; }
    public List<FinanceMonthPointDto> MonthlyTrend { get; set; } = new();
    public List<FinanceCategoryAmountDto> IncomeBySource { get; set; } = new();
    public List<FinanceCategoryAmountDto> ExpenseByCategory { get; set; } = new();
    public List<FinanceTransactionDto> RecentTransactions { get; set; } = new();
}

public record GetFinanceOverviewQuery(int SocietyId) : IRequest<FinanceOverviewDto>;

public class FinanceOverviewQueryHandler : IRequestHandler<GetFinanceOverviewQuery, FinanceOverviewDto>
{
    private readonly IApplicationDbContext _context;

    public FinanceOverviewQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<FinanceOverviewDto> Handle(GetFinanceOverviewQuery request, CancellationToken ct)
    {
        // All-time — matches the flat absolute totals the Overview cards are
        // meant to show; the Income/Expenses/Ledger/Reports tabs are where
        // date-range scoping happens.
        var income = await FinanceQueryHelpers.GetIncomeRowsAsync(_context, request.SocietyId, null, null, ct);
        var expenses = await FinanceQueryHelpers.GetExpenseRowsAsync(_context, request.SocietyId, null, null, ct);
        var outstanding = await FinanceQueryHelpers.GetOutstandingRowsAsync(_context, request.SocietyId, ct);

        var totalIncome = income.Sum(r => r.Amount);
        var totalExpense = expenses.Sum(r => r.Amount);

        var today = DateTime.UtcNow.Date;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var sixMonthsAgo = currentMonth.AddMonths(-5);

        var monthlyTrend = Enumerable.Range(0, 6)
            .Select(offset => sixMonthsAgo.AddMonths(offset))
            .Select(month => new FinanceMonthPointDto
            {
                MonthLabel = month.ToString("MMM yyyy"),
                Income = income.Where(r => r.Date.Year == month.Year && r.Date.Month == month.Month).Sum(r => r.Amount),
                Expense = expenses.Where(r => r.ExpenseDate.Year == month.Year && r.ExpenseDate.Month == month.Month).Sum(r => r.Amount)
            })
            .ToList();

        var incomeBySource = new List<FinanceCategoryAmountDto>
        {
            new() { Label = "Maintenance", Amount = income.Where(r => r.Source == FinanceSource.Maintenance).Sum(r => r.Amount) },
            new() { Label = "Festival", Amount = income.Where(r => r.Source == FinanceSource.Festival).Sum(r => r.Amount) },
            new() { Label = "Water Tanker", Amount = income.Where(r => r.Source == FinanceSource.WaterTanker).Sum(r => r.Amount) }
        };

        var expenseByCategory = expenses
            .GroupBy(e => e.CategoryLabel)
            .Select(g => new FinanceCategoryAmountDto { Label = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(c => c.Amount)
            .ToList();

        var recentTransactions = income
            .Select(r => new FinanceTransactionDto { Date = r.Date, Type = "Income", Source = FinanceQueryHelpers.SourceLabel(r.Source), Description = r.Description, Amount = r.Amount })
            .Concat(expenses.Select(e => new FinanceTransactionDto { Date = e.ExpenseDate, Type = "Expense", Source = FinanceQueryHelpers.SourceLabel(e.Source), Description = e.Title, Amount = e.Amount }))
            .OrderByDescending(t => t.Date)
            .Take(10)
            .ToList();

        return new FinanceOverviewDto
        {
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            AvailableBalance = totalIncome - totalExpense,
            PendingCollection = outstanding.Sum(o => o.Amount),
            MonthlyTrend = monthlyTrend,
            IncomeBySource = incomeBySource,
            ExpenseByCategory = expenseByCategory,
            RecentTransactions = recentTransactions
        };
    }
}
