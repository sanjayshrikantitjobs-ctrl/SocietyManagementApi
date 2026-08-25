using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Finance;

public class FinanceReportSummaryDto
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetBalance { get; set; }
    public List<FinanceCategoryAmountDto> IncomeBySource { get; set; } = new();
    public List<FinanceCategoryAmountDto> ExpenseByCategory { get; set; } = new();
}

public record GetFinanceReportSummaryQuery(int SocietyId, DateTime? DateFrom, DateTime? DateTo) : IRequest<FinanceReportSummaryDto>;

public record GetFinanceReportPdfQuery(int SocietyId, DateTime? DateFrom, DateTime? DateTo) : IRequest<byte[]>;

public record GetFinanceReportExcelQuery(int SocietyId, DateTime? DateFrom, DateTime? DateTo) : IRequest<byte[]>;

public class FinanceReportsQueryHandlers :
    IRequestHandler<GetFinanceReportSummaryQuery, FinanceReportSummaryDto>,
    IRequestHandler<GetFinanceReportPdfQuery, byte[]>,
    IRequestHandler<GetFinanceReportExcelQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinanceReportService _reportService;

    public FinanceReportsQueryHandlers(IApplicationDbContext context, IFinanceReportService reportService)
    {
        _context = context;
        _reportService = reportService;
    }

    public async Task<FinanceReportSummaryDto> Handle(GetFinanceReportSummaryQuery request, CancellationToken ct) =>
        await BuildSummaryAsync(request.SocietyId, request.DateFrom, request.DateTo, ct);

    public async Task<byte[]> Handle(GetFinanceReportPdfQuery request, CancellationToken ct)
    {
        var data = await BuildReportDataAsync(request.SocietyId, request.DateFrom, request.DateTo, ct);
        return _reportService.GeneratePdf(data);
    }

    public async Task<byte[]> Handle(GetFinanceReportExcelQuery request, CancellationToken ct)
    {
        var data = await BuildReportDataAsync(request.SocietyId, request.DateFrom, request.DateTo, ct);
        return _reportService.GenerateExcel(data);
    }

    private async Task<FinanceReportSummaryDto> BuildSummaryAsync(int societyId, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        var income = await FinanceQueryHelpers.GetIncomeRowsAsync(_context, societyId, dateFrom, dateTo, ct);
        var expenses = await FinanceQueryHelpers.GetExpenseRowsAsync(_context, societyId, dateFrom, dateTo, ct);

        var totalIncome = income.Sum(r => r.Amount);
        var totalExpense = expenses.Sum(r => r.Amount);

        return new FinanceReportSummaryDto
        {
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            NetBalance = totalIncome - totalExpense,
            IncomeBySource = new List<FinanceCategoryAmountDto>
            {
                new() { Label = "Maintenance", Amount = income.Where(r => r.Source == FinanceSource.Maintenance).Sum(r => r.Amount) },
                new() { Label = "Festival", Amount = income.Where(r => r.Source == FinanceSource.Festival).Sum(r => r.Amount) },
                new() { Label = "Water Tanker", Amount = income.Where(r => r.Source == FinanceSource.WaterTanker).Sum(r => r.Amount) }
            },
            ExpenseByCategory = expenses
                .GroupBy(e => e.CategoryLabel)
                .Select(g => new FinanceCategoryAmountDto { Label = g.Key, Amount = g.Sum(e => e.Amount) })
                .OrderByDescending(c => c.Amount)
                .ToList()
        };
    }

    private async Task<FinanceReportData> BuildReportDataAsync(int societyId, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        var society = await _context.Societies.Where(s => s.Id == societyId && !s.IsDeleted)
            .Select(s => new { s.Name, s.LogoUrl }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Society), societyId);

        var summary = await BuildSummaryAsync(societyId, dateFrom, dateTo, ct);

        return new FinanceReportData(
            society.Name, society.LogoUrl, dateFrom, dateTo, summary.TotalIncome, summary.TotalExpense, summary.NetBalance,
            summary.IncomeBySource.Select(x => new FinanceReportLine(x.Label, x.Amount)).ToList(),
            summary.ExpenseByCategory.Select(x => new FinanceReportLine(x.Label, x.Amount)).ToList());
    }
}
