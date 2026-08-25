using MediatR;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Finance;

public record GetFinanceOutstandingQuery(
    int SocietyId, FinanceSource? Source, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FinanceOutstandingRowDto>>;

public class FinanceOutstandingQueryHandler : IRequestHandler<GetFinanceOutstandingQuery, PaginatedResult<FinanceOutstandingRowDto>>
{
    private readonly IApplicationDbContext _context;

    public FinanceOutstandingQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedResult<FinanceOutstandingRowDto>> Handle(GetFinanceOutstandingQuery request, CancellationToken ct)
    {
        var rows = await FinanceQueryHelpers.GetOutstandingRowsAsync(_context, request.SocietyId, ct);

        IEnumerable<FinanceOutstandingRowDto> filtered = rows;
        if (request.Source.HasValue) filtered = filtered.Where(r => r.Source == request.Source);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(r => (r.FlatNumber != null && r.FlatNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
                || r.PayerName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var all = filtered.OrderByDescending(r => r.Amount).ToList();
        var totalCount = all.Count;
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedResult<FinanceOutstandingRowDto>(items, totalCount, pageNumber, pageSize);
    }
}
