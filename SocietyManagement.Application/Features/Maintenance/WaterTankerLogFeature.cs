using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Maintenance;

public class WaterTankerLogDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string ProviderName { get; set; } = default!;
    public string VehicleNumber { get; set; } = default!;
    public int NumberOfTankers { get; set; }
    public decimal PricePerTanker { get; set; }
    public decimal TotalAmount => NumberOfTankers * PricePerTanker;
    public string? Notes { get; set; }
}

/// <summary>The month-picker's small dashboard — how many tankers were
/// called and what they cost, for whichever month is selected.</summary>
public class WaterTankerLogMonthSummaryDto
{
    public int TotalDeliveries { get; set; }
    public int TotalTankers { get; set; }
    public decimal TotalAmount { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateWaterTankerLogCommand(
    int SocietyId, DateTime Date, string ProviderName, string VehicleNumber,
    int NumberOfTankers, decimal PricePerTanker, string? Notes) : IRequest<int>;

public class CreateWaterTankerLogCommandValidator : AbstractValidator<CreateWaterTankerLogCommand>
{
    public CreateWaterTankerLogCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.ProviderName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VehicleNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NumberOfTankers).GreaterThan(0);
        RuleFor(x => x.PricePerTanker).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public record UpdateWaterTankerLogCommand(
    int Id, DateTime Date, string ProviderName, string VehicleNumber,
    int NumberOfTankers, decimal PricePerTanker, string? Notes) : IRequest<Unit>;

public class UpdateWaterTankerLogCommandValidator : AbstractValidator<UpdateWaterTankerLogCommand>
{
    public UpdateWaterTankerLogCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ProviderName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VehicleNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NumberOfTankers).GreaterThan(0);
        RuleFor(x => x.PricePerTanker).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public record DeleteWaterTankerLogCommand(int Id) : IRequest<Unit>;

public class WaterTankerLogCommandHandlers :
    IRequestHandler<CreateWaterTankerLogCommand, int>,
    IRequestHandler<UpdateWaterTankerLogCommand, Unit>,
    IRequestHandler<DeleteWaterTankerLogCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public WaterTankerLogCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateWaterTankerLogCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var log = new WaterTankerLog
        {
            SocietyId = request.SocietyId, Date = request.Date.Date, ProviderName = request.ProviderName,
            VehicleNumber = request.VehicleNumber, NumberOfTankers = request.NumberOfTankers,
            PricePerTanker = request.PricePerTanker, Notes = request.Notes
        };
        await _context.WaterTankerLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Maintenance", nameof(WaterTankerLog), log.Id.ToString(), ct: ct);
        return log.Id;
    }

    public async Task<Unit> Handle(UpdateWaterTankerLogCommand request, CancellationToken ct)
    {
        var log = await _context.WaterTankerLogs.FirstOrDefaultAsync(w => w.Id == request.Id && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(WaterTankerLog), request.Id);

        log.Date = request.Date.Date;
        log.ProviderName = request.ProviderName;
        log.VehicleNumber = request.VehicleNumber;
        log.NumberOfTankers = request.NumberOfTankers;
        log.PricePerTanker = request.PricePerTanker;
        log.Notes = request.Notes;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Maintenance", nameof(WaterTankerLog), log.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteWaterTankerLogCommand request, CancellationToken ct)
    {
        var log = await _context.WaterTankerLogs.FirstOrDefaultAsync(w => w.Id == request.Id && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(WaterTankerLog), request.Id);

        log.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Maintenance", nameof(WaterTankerLog), log.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetWaterTankerLogsQuery(
    int SocietyId, DateTime Month, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<WaterTankerLogDto>>;

public record GetWaterTankerLogSummaryQuery(int SocietyId, DateTime Month) : IRequest<WaterTankerLogMonthSummaryDto>;

public class WaterTankerLogQueryHandlers :
    IRequestHandler<GetWaterTankerLogsQuery, PaginatedResult<WaterTankerLogDto>>,
    IRequestHandler<GetWaterTankerLogSummaryQuery, WaterTankerLogMonthSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public WaterTankerLogQueryHandlers(IApplicationDbContext context) => _context = context;

    private static (DateTime start, DateTime end) MonthRange(DateTime month)
    {
        var start = new DateTime(month.Year, month.Month, 1);
        return (start, start.AddMonths(1));
    }

    public async Task<PaginatedResult<WaterTankerLogDto>> Handle(GetWaterTankerLogsQuery request, CancellationToken ct)
    {
        var (start, end) = MonthRange(request.Month);
        var query = _context.WaterTankerLogs.Where(w => w.SocietyId == request.SocietyId && w.Date >= start && w.Date < end);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(w => w.ProviderName.ToLower().Contains(term) || w.VehicleNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderByDescending(w => w.Date).ThenByDescending(w => w.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WaterTankerLogDto
            {
                Id = w.Id, Date = w.Date, ProviderName = w.ProviderName, VehicleNumber = w.VehicleNumber,
                NumberOfTankers = w.NumberOfTankers, PricePerTanker = w.PricePerTanker, Notes = w.Notes
            })
            .ToListAsync(ct);

        return new PaginatedResult<WaterTankerLogDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<WaterTankerLogMonthSummaryDto> Handle(GetWaterTankerLogSummaryQuery request, CancellationToken ct)
    {
        var (start, end) = MonthRange(request.Month);
        var rows = await _context.WaterTankerLogs
            .Where(w => w.SocietyId == request.SocietyId && w.Date >= start && w.Date < end)
            .Select(w => new { w.NumberOfTankers, w.PricePerTanker })
            .ToListAsync(ct);

        return new WaterTankerLogMonthSummaryDto
        {
            TotalDeliveries = rows.Count,
            TotalTankers = rows.Sum(r => r.NumberOfTankers),
            TotalAmount = rows.Sum(r => r.NumberOfTankers * r.PricePerTanker)
        };
    }
}
