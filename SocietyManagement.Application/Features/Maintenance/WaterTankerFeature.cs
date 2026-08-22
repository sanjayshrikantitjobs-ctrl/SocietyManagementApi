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

public class WaterTankerCollectionDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public DateTime Month { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? Notes { get; set; }
}

public class WaterTankerMonthSummaryDto
{
    public int TotalFlats { get; set; }
    public int FlatsPaidCount { get; set; }
    public int FlatsPendingCount { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalPending { get; set; }
}

// ---- Commands ----------------------------------------------------------------
/// <summary>Bulk-creates one charge row per flat in the society for a given
/// month — the recurring-obligation generation step, run once a month
/// (manually for now; a scheduled job can call this later the same way
/// MaintenanceBillGenerationService does for bills).</summary>
public record GenerateWaterTankerChargesCommand(int SocietyId, DateTime Month, decimal Amount) : IRequest<int>;

public class GenerateWaterTankerChargesCommandValidator : AbstractValidator<GenerateWaterTankerChargesCommand>
{
    public GenerateWaterTankerChargesCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public record RecordWaterTankerPaymentCommand(int Id, DateTime PaymentDate, string? Notes) : IRequest<Unit>;

public class RecordWaterTankerPaymentCommandValidator : AbstractValidator<RecordWaterTankerPaymentCommand>
{
    public RecordWaterTankerPaymentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class WaterTankerCommandHandlers :
    IRequestHandler<GenerateWaterTankerChargesCommand, int>,
    IRequestHandler<RecordWaterTankerPaymentCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public WaterTankerCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(GenerateWaterTankerChargesCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var month = new DateTime(request.Month.Year, request.Month.Month, 1);

        var flatIds = await _context.Flats
            .Where(f => f.Floor.Wing.Building.SocietyId == request.SocietyId && !f.IsDeleted)
            .Select(f => f.Id)
            .ToListAsync(ct);

        var existingFlatIds = await _context.WaterTankerCollections
            .Where(w => w.SocietyId == request.SocietyId && w.Month == month)
            .Select(w => w.FlatId)
            .ToListAsync(ct);

        var toCreate = flatIds.Except(existingFlatIds)
            .Select(flatId => new WaterTankerCollection
            {
                SocietyId = request.SocietyId, FlatId = flatId, Month = month, Amount = request.Amount, IsPaid = false
            })
            .ToList();

        if (toCreate.Count > 0)
        {
            await _context.WaterTankerCollections.AddRangeAsync(toCreate, ct);
            await _context.SaveChangesAsync(ct);
        }

        await _auditService.LogAsync(AuditAction.Create, "Maintenance", nameof(WaterTankerCollection),
            $"{request.SocietyId}-{month:yyyy-MM}", newValues: new { request.Amount, FlatsAffected = toCreate.Count }, ct: ct);

        return toCreate.Count;
    }

    public async Task<Unit> Handle(RecordWaterTankerPaymentCommand request, CancellationToken ct)
    {
        var record = await _context.WaterTankerCollections.FirstOrDefaultAsync(w => w.Id == request.Id && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(WaterTankerCollection), request.Id);

        record.IsPaid = true;
        record.PaymentDate = request.PaymentDate;
        if (!string.IsNullOrWhiteSpace(request.Notes)) record.Notes = request.Notes;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Payment, "Maintenance", nameof(WaterTankerCollection), record.Id.ToString(), ct: ct);

        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetWaterTankerCollectionsQuery(
    int SocietyId, DateTime Month, bool? IsPaid, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<WaterTankerCollectionDto>>;

public record GetWaterTankerMonthsQuery(int SocietyId) : IRequest<List<DateTime>>;

public record GetWaterTankerSummaryQuery(int SocietyId, DateTime Month) : IRequest<WaterTankerMonthSummaryDto>;

/// <summary>Resident self-service: every water tanker charge for every flat
/// the current user currently resides at, across all months — mirrors
/// VisitorVisitFeature's EnsureCurrentUserResidesAtAsync pattern (resolve
/// the caller's own flats server-side, never trust a client-supplied
/// FlatId) so a Member can check their own charge history without needing
/// the Admin-only Maintenance module.</summary>
public record GetMyWaterTankerCollectionsQuery : IRequest<List<WaterTankerCollectionDto>>;

public class WaterTankerQueryHandlers :
    IRequestHandler<GetWaterTankerCollectionsQuery, PaginatedResult<WaterTankerCollectionDto>>,
    IRequestHandler<GetWaterTankerMonthsQuery, List<DateTime>>,
    IRequestHandler<GetWaterTankerSummaryQuery, WaterTankerMonthSummaryDto>,
    IRequestHandler<GetMyWaterTankerCollectionsQuery, List<WaterTankerCollectionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public WaterTankerQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<WaterTankerCollectionDto>> Handle(GetMyWaterTankerCollectionsQuery request, CancellationToken ct)
    {
        var flatIds = await _context.Members
            .Where(m => m.UserId == _currentUserService.UserId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .Where(r => !r.IsDeleted && r.MoveOutDate == null)
            .Select(r => r.FlatId)
            .Distinct()
            .ToListAsync(ct);

        return await _context.WaterTankerCollections
            .Where(w => flatIds.Contains(w.FlatId))
            .OrderByDescending(w => w.Month)
            .Select(w => new WaterTankerCollectionDto
            {
                Id = w.Id, FlatId = w.FlatId, FlatNumber = w.Flat.FlatNumber, Month = w.Month,
                Amount = w.Amount, IsPaid = w.IsPaid, PaymentDate = w.PaymentDate, Notes = w.Notes
            })
            .ToListAsync(ct);
    }

    public async Task<PaginatedResult<WaterTankerCollectionDto>> Handle(GetWaterTankerCollectionsQuery request, CancellationToken ct)
    {
        var month = new DateTime(request.Month.Year, request.Month.Month, 1);
        var query = _context.WaterTankerCollections.Where(w => w.SocietyId == request.SocietyId && w.Month == month);

        if (request.IsPaid.HasValue) query = query.Where(w => w.IsPaid == request.IsPaid.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(w => w.Flat.FlatNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderBy(w => w.Flat.FlatNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WaterTankerCollectionDto
            {
                Id = w.Id, FlatId = w.FlatId, FlatNumber = w.Flat.FlatNumber, Month = w.Month,
                Amount = w.Amount, IsPaid = w.IsPaid, PaymentDate = w.PaymentDate, Notes = w.Notes
            })
            .ToListAsync(ct);

        return new PaginatedResult<WaterTankerCollectionDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<List<DateTime>> Handle(GetWaterTankerMonthsQuery request, CancellationToken ct) =>
        await _context.WaterTankerCollections
            .Where(w => w.SocietyId == request.SocietyId)
            .Select(w => w.Month)
            .Distinct()
            .OrderByDescending(m => m)
            .ToListAsync(ct);

    public async Task<WaterTankerMonthSummaryDto> Handle(GetWaterTankerSummaryQuery request, CancellationToken ct)
    {
        var month = new DateTime(request.Month.Year, request.Month.Month, 1);
        var rows = await _context.WaterTankerCollections
            .Where(w => w.SocietyId == request.SocietyId && w.Month == month)
            .Select(w => new { w.Amount, w.IsPaid })
            .ToListAsync(ct);

        return new WaterTankerMonthSummaryDto
        {
            TotalFlats = rows.Count,
            FlatsPaidCount = rows.Count(r => r.IsPaid),
            FlatsPendingCount = rows.Count(r => !r.IsPaid),
            TotalCollected = rows.Where(r => r.IsPaid).Sum(r => r.Amount),
            TotalPending = rows.Where(r => !r.IsPaid).Sum(r => r.Amount)
        };
    }
}
