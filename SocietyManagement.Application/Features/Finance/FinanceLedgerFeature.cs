using MediatR;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Shared.Constants;

namespace SocietyManagement.Application.Features.Finance;

public class FinanceLedgerRowDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = default!; // "Income" | "Expense"
    public string Source { get; set; } = default!;
    public string Description { get; set; } = default!;
    /// <summary>Positive for Income, negative for Expense.</summary>
    public decimal Amount { get; set; }
    /// <summary>Cumulative balance as of this transaction, in true
    /// chronological order — independent of how the page is displayed.</summary>
    public decimal RunningBalance { get; set; }
}

public class FinanceLedgerPageDto
{
    public List<FinanceLedgerRowDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    /// <summary>Running balance as of just before DateFrom — 0 when no
    /// DateFrom filter is applied (the ledger then starts from day one).</summary>
    public decimal OpeningBalance { get; set; }
}

public record GetFinanceLedgerQuery(
    int SocietyId, DateTime? DateFrom, DateTime? DateTo,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<FinanceLedgerPageDto>;

public class FinanceLedgerQueryHandler : IRequestHandler<GetFinanceLedgerQuery, FinanceLedgerPageDto>
{
    private readonly IApplicationDbContext _context;

    public FinanceLedgerQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<FinanceLedgerPageDto> Handle(GetFinanceLedgerQuery request, CancellationToken ct)
    {
        // Fetch all-time (not date-filtered yet) so the running balance and
        // opening balance are correct even when DateFrom excludes earlier
        // transactions — the balance must still reflect everything before it.
        var income = await FinanceQueryHelpers.GetIncomeRowsAsync(_context, request.SocietyId, null, null, ct);
        var expenses = await FinanceQueryHelpers.GetExpenseRowsAsync(_context, request.SocietyId, null, null, ct);

        var chronological = income
            .Select(r => new FinanceLedgerRowDto { Date = r.Date, Type = "Income", Source = FinanceQueryHelpers.SourceLabel(r.Source), Description = r.Description, Amount = r.Amount })
            .Concat(expenses.Select(e => new FinanceLedgerRowDto { Date = e.ExpenseDate, Type = "Expense", Source = FinanceQueryHelpers.SourceLabel(e.Source), Description = e.Title, Amount = -e.Amount }))
            .OrderBy(x => x.Date)
            .ToList();

        decimal running = 0;
        foreach (var entry in chronological)
        {
            running += entry.Amount;
            entry.RunningBalance = running;
        }

        var openingBalance = request.DateFrom.HasValue
            ? chronological.LastOrDefault(x => x.Date < request.DateFrom.Value)?.RunningBalance ?? 0
            : 0;

        // Newest-first for display, matching every other list in this app —
        // each row's RunningBalance still reflects its true chronological total.
        var filtered = chronological
            .Where(x => (!request.DateFrom.HasValue || x.Date >= request.DateFrom) && (!request.DateTo.HasValue || x.Date <= request.DateTo))
            .OrderByDescending(x => x.Date)
            .ToList();

        var totalCount = filtered.Count;
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new FinanceLedgerPageDto
        {
            Items = items, TotalCount = totalCount, PageNumber = pageNumber, PageSize = pageSize, OpeningBalance = openingBalance
        };
    }
}
