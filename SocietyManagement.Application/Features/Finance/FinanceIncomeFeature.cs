using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Finance;

public record GetFinanceIncomeQuery(
    int SocietyId, FinanceSource? Source, DateTime? DateFrom, DateTime? DateTo, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FinanceIncomeRowDto>>;

/// <summary>Also doubles as the Receipts tab's data source — every income
/// row already carries a ReceiptNumber (real for Festival, synthesized
/// "MNT-"/"WTR-" for Maintenance/WaterTanker), so there's no separate
/// receipts query, just this one filtered/sorted the same way.</summary>
public record GetFinanceReceiptPdfQuery(FinanceSource Source, int Id) : IRequest<byte[]>;

public class FinanceIncomeQueryHandlers :
    IRequestHandler<GetFinanceIncomeQuery, PaginatedResult<FinanceIncomeRowDto>>,
    IRequestHandler<GetFinanceReceiptPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;
    private readonly IPdfReceiptService _pdfReceiptService;

    public FinanceIncomeQueryHandlers(IApplicationDbContext context, IMediator mediator, IPdfReceiptService pdfReceiptService)
    {
        _context = context;
        _mediator = mediator;
        _pdfReceiptService = pdfReceiptService;
    }

    public async Task<PaginatedResult<FinanceIncomeRowDto>> Handle(GetFinanceIncomeQuery request, CancellationToken ct)
    {
        var rows = await FinanceQueryHelpers.GetIncomeRowsAsync(_context, request.SocietyId, request.DateFrom, request.DateTo, ct);

        IEnumerable<FinanceIncomeRowDto> filtered = rows;
        if (request.Source.HasValue) filtered = filtered.Where(r => r.Source == request.Source);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(r => r.PayerName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (r.FlatNumber != null && r.FlatNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
                || r.ReceiptNumber.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var all = filtered.OrderByDescending(r => r.Date).ToList();
        var totalCount = all.Count;
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedResult<FinanceIncomeRowDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<byte[]> Handle(GetFinanceReceiptPdfQuery request, CancellationToken ct)
    {
        // Festival contributions already have a real receipt PDF endpoint —
        // reuse it rather than duplicating the layout/target-amount logic.
        if (request.Source == FinanceSource.Festival)
        {
            return await _mediator.Send(new GetContributionReceiptPdfQuery(request.Id), ct);
        }

        if (request.Source == FinanceSource.Maintenance)
        {
            var data = await _context.MaintenancePayments
                .Where(p => p.Id == request.Id && !p.IsDeleted)
                .Select(p => new
                {
                    SocietyName = p.MaintenanceBill.Flat.Floor.Wing.Building.Society.Name,
                    SocietyLogoUrl = p.MaintenanceBill.Flat.Floor.Wing.Building.Society.LogoUrl,
                    PayerName = p.MaintenanceBill.OwnerNameSnapshot ?? p.MaintenanceBill.Flat.FlatNumber,
                    FlatNumber = p.MaintenanceBill.Flat.FlatNumber,
                    p.Amount,
                    PaymentMethod = p.PaymentMode.ToString(),
                    p.PaymentDate,
                    BillMonth = p.MaintenanceBill.BillMonth,
                    p.MaintenanceBill.InvoiceNumber
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(MaintenancePayment), request.Id);

            return _pdfReceiptService.GenerateFinanceReceipt(new FinanceReceiptData(
                "MNT-" + request.Id, data.SocietyName, data.SocietyLogoUrl, "Maintenance Payment", data.PayerName,
                data.FlatNumber, data.Amount, data.PaymentMethod, data.PaymentDate,
                $"Maintenance for {data.BillMonth:MMM yyyy} (Invoice {data.InvoiceNumber})"));
        }

        // WaterTanker
        var tanker = await _context.WaterTankerCollections
            .Where(w => w.Id == request.Id && !w.IsDeleted && w.IsPaid)
            .Select(w => new
            {
                SocietyName = w.Society.Name,
                SocietyLogoUrl = w.Society.LogoUrl,
                FlatNumber = w.Flat.FlatNumber,
                w.Amount,
                PaymentDate = w.PaymentDate!.Value,
                w.Month
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(WaterTankerCollection), request.Id);

        return _pdfReceiptService.GenerateFinanceReceipt(new FinanceReceiptData(
            "WTR-" + request.Id, tanker.SocietyName, tanker.SocietyLogoUrl, "Water Tanker Payment", tanker.FlatNumber,
            tanker.FlatNumber, tanker.Amount, null, tanker.PaymentDate, $"Water Tanker for {tanker.Month:MMM yyyy}"));
    }
}
