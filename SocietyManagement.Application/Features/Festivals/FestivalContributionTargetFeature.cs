using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Festivals;

public class FlatContributionDto
{
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public decimal TargetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public FlatContributionStatus Status { get; set; }
    /// <summary>Non-null when the flat has told the society it won't be
    /// contributing — a memo alongside Status, not a replacement for it.</summary>
    public string? DeclineReason { get; set; }
    /// <summary>How the flat's most recent contribution was paid — null if
    /// none recorded yet. A flat can pay in installments via different
    /// methods; this is only the latest one, not every method ever used.</summary>
    public ContributionPaymentMethod? LastPaymentMethod { get; set; }
}

public class FlatContributionKpisDto
{
    public int TotalFlats { get; set; }
    public decimal TotalTargetAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public int FlatsPaidCount { get; set; }
    public int FlatsPartiallyPaidCount { get; set; }
    public int FlatsPendingCount { get; set; }
    public int FlatsNoTargetCount { get; set; }
}

// ---- Commands ----------------------------------------------------------------
/// <summary>Sets (or overwrites) the same annual target on every flat in the
/// society for this festival — the common case ("every flat gives ₹5000").
/// Individual flats (e.g. vacant units) can be adjusted afterward via
/// UpdateFlatContributionTargetCommand.</summary>
public record SetContributionTargetsCommand(int FestivalId, decimal TargetAmount) : IRequest<int>;

public class SetContributionTargetsCommandValidator : AbstractValidator<SetContributionTargetsCommand>
{
    public SetContributionTargetsCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.TargetAmount).GreaterThanOrEqualTo(0);
    }
}

public record UpdateFlatContributionTargetCommand(int FestivalId, int FlatId, decimal TargetAmount) : IRequest<Unit>;

public class UpdateFlatContributionTargetCommandValidator : AbstractValidator<UpdateFlatContributionTargetCommand>
{
    public UpdateFlatContributionTargetCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.TargetAmount).GreaterThanOrEqualTo(0);
    }
}

/// <summary>Records (or clears, by passing null/empty) why a flat says it
/// won't be contributing — a plain memo, not a status change: the flat's
/// computed FlatContributionStatus is untouched, so a "declined" flat can
/// still record a contribution later without anyone undoing this first.
/// Upserts the same FestivalFlatTarget row targets live in, defaulting
/// TargetAmount to 0 only when creating a brand new row (a flat can be
/// marked "declined" before any target has ever been set on it).</summary>
public record SetFlatDeclineReasonCommand(int FestivalId, int FlatId, string? Reason) : IRequest<Unit>;

public class SetFlatDeclineReasonCommandValidator : AbstractValidator<SetFlatDeclineReasonCommand>
{
    public SetFlatDeclineReasonCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class ContributionTargetCommandHandlers :
    IRequestHandler<SetContributionTargetsCommand, int>,
    IRequestHandler<UpdateFlatContributionTargetCommand, Unit>,
    IRequestHandler<SetFlatDeclineReasonCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public ContributionTargetCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(SetContributionTargetsCommand request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        var flatIds = await _context.Flats
            .Where(fl => fl.Floor.Wing.Building.SocietyId == festival.SocietyId && !fl.IsDeleted)
            .Select(fl => fl.Id)
            .ToListAsync(ct);

        var existingTargets = await _context.FestivalFlatTargets
            .Where(t => t.FestivalId == request.FestivalId)
            .ToDictionaryAsync(t => t.FlatId, ct);

        foreach (var flatId in flatIds)
        {
            if (existingTargets.TryGetValue(flatId, out var existing))
            {
                existing.TargetAmount = request.TargetAmount;
            }
            else
            {
                await _context.FestivalFlatTargets.AddAsync(
                    new FestivalFlatTarget { FestivalId = request.FestivalId, FlatId = flatId, TargetAmount = request.TargetAmount }, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalFlatTarget), request.FestivalId.ToString(),
            newValues: new { request.TargetAmount, FlatsAffected = flatIds.Count }, ct: ct);

        return flatIds.Count;
    }

    public async Task<Unit> Handle(UpdateFlatContributionTargetCommand request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        if (!await _context.Flats.AnyAsync(
                fl => fl.Id == request.FlatId && fl.Floor.Wing.Building.SocietyId == festival.SocietyId && !fl.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }

        var target = await _context.FestivalFlatTargets
            .FirstOrDefaultAsync(t => t.FestivalId == request.FestivalId && t.FlatId == request.FlatId, ct);

        if (target is null)
        {
            await _context.FestivalFlatTargets.AddAsync(
                new FestivalFlatTarget { FestivalId = request.FestivalId, FlatId = request.FlatId, TargetAmount = request.TargetAmount }, ct);
        }
        else
        {
            target.TargetAmount = request.TargetAmount;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalFlatTarget),
            $"{request.FestivalId}-{request.FlatId}", newValues: new { request.TargetAmount }, ct: ct);

        return Unit.Value;
    }

    public async Task<Unit> Handle(SetFlatDeclineReasonCommand request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        if (!await _context.Flats.AnyAsync(
                fl => fl.Id == request.FlatId && fl.Floor.Wing.Building.SocietyId == festival.SocietyId && !fl.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }

        var target = await _context.FestivalFlatTargets
            .FirstOrDefaultAsync(t => t.FestivalId == request.FestivalId && t.FlatId == request.FlatId, ct);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (target is null)
        {
            await _context.FestivalFlatTargets.AddAsync(
                new FestivalFlatTarget { FestivalId = request.FestivalId, FlatId = request.FlatId, TargetAmount = 0m, DeclineReason = reason }, ct);
        }
        else
        {
            target.DeclineReason = reason;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalFlatTarget),
            $"{request.FestivalId}-{request.FlatId}", newValues: new { DeclineReason = reason }, ct: ct);

        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
/// <summary>Statuses is a multi-select — pass more than one (e.g. Pending +
/// PartiallyPaid) to see everyone who hasn't fully paid yet in one view;
/// omitted/empty means every status.</summary>
public record GetFlatContributionsQuery(
    int FestivalId, string? Search, List<FlatContributionStatus>? Statuses,
    string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FlatContributionDto>>;

public record GetFlatContributionKpisQuery(int FestivalId) : IRequest<FlatContributionKpisDto>;

/// <summary>The "sum after filters" companion to GetFlatContributionsQuery —
/// same Search/Statuses filter, but aggregated over every matching flat
/// rather than just the current page, since the page the UI holds
/// client-side is never the whole filtered set once pagination kicks in.</summary>
public record GetFlatContributionsSumQuery(int FestivalId, string? Search, List<FlatContributionStatus>? Statuses)
    : IRequest<FlatContributionsSumDto>;

public class FlatContributionsSumDto
{
    public int FlatCount { get; set; }
    public decimal TotalTargetAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
}

/// <summary>Powers the "Record Contribution" flat dropdown — every flat in
/// the festival's society whose contribution isn't fully settled yet
/// (Pending, PartiallyPaid, or NoTarget), ordered by flat number. Unlike
/// GetFlatContributionsQuery this is deliberately unpaginated (dropdowns
/// need the whole list, not a page of it) but reuses the same status
/// computation so the two never disagree about who's already paid.</summary>
public record GetContributableFlatsQuery(int FestivalId) : IRequest<List<ContributableFlatDto>>;

public class ContributableFlatDto
{
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
}

public class FlatContributionQueryHandlers :
    IRequestHandler<GetFlatContributionsQuery, PaginatedResult<FlatContributionDto>>,
    IRequestHandler<GetFlatContributionKpisQuery, FlatContributionKpisDto>,
    IRequestHandler<GetFlatContributionsSumQuery, FlatContributionsSumDto>,
    IRequestHandler<GetContributableFlatsQuery, List<ContributableFlatDto>>
{
    private readonly IApplicationDbContext _context;

    public FlatContributionQueryHandlers(IApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<List<FlatContributionDto>> BuildFlatContributionsAsync(int festivalId, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == festivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), festivalId);

        var flats = await _context.Flats
            .Where(fl => fl.Floor.Wing.Building.SocietyId == festival.SocietyId && !fl.IsDeleted)
            .Select(fl => new { fl.Id, fl.FlatNumber })
            .ToListAsync(ct);

        var targets = await _context.FestivalFlatTargets
            .Where(t => t.FestivalId == festivalId)
            .Select(t => new { t.FlatId, t.TargetAmount, t.DeclineReason })
            .ToDictionaryAsync(t => t.FlatId, ct);

        var paidAmounts = await _context.FestivalContributions
            .Where(c => c.FestivalId == festivalId && c.FlatId != null)
            .GroupBy(c => c.FlatId!.Value)
            .Select(g => new { FlatId = g.Key, Paid = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.FlatId, x => x.Paid, ct);

        // Materialized then grouped in memory (not via a GroupBy+OrderBy+First
        // SQL translation) — that combination is fragile across EF Core
        // versions/providers, and a festival's contribution rows are few
        // enough that pulling them client-side is cheap.
        var contributionRows = await _context.FestivalContributions
            .Where(c => c.FestivalId == festivalId && c.FlatId != null)
            .Select(c => new { FlatId = c.FlatId!.Value, c.PaymentDate, c.CreatedAt, c.PaymentMethod })
            .ToListAsync(ct);
        var lastPaymentMethods = contributionRows
            .GroupBy(c => c.FlatId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.PaymentDate).ThenByDescending(c => c.CreatedAt).First().PaymentMethod);

        return flats.Select(fl =>
        {
            var targetInfo = targets.GetValueOrDefault(fl.Id);
            var target = targetInfo?.TargetAmount ?? 0m;
            var paid = paidAmounts.GetValueOrDefault(fl.Id, 0m);
            var outstanding = Math.Max(target - paid, 0m);
            var status = target == 0
                ? FlatContributionStatus.NoTarget
                : paid <= 0
                    ? FlatContributionStatus.Pending
                    : paid < target
                        ? FlatContributionStatus.PartiallyPaid
                        : FlatContributionStatus.Paid;

            return new FlatContributionDto
            {
                FlatId = fl.Id, FlatNumber = fl.FlatNumber, TargetAmount = target, PaidAmount = paid,
                OutstandingAmount = outstanding, Status = status, DeclineReason = targetInfo?.DeclineReason,
                LastPaymentMethod = lastPaymentMethods.TryGetValue(fl.Id, out var method) ? method : null
            };
        }).OrderBy(a=>a.FlatId).ToList();
    }

    /// <summary>Shared by GetFlatContributionsQuery and its "sum" companion
    /// so the two can never disagree about which flats match a given
    /// search/status filter.</summary>
    private static List<FlatContributionDto> ApplyFilters(List<FlatContributionDto> all, string? search, List<FlatContributionStatus>? statuses)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            all = all.Where(f => f.FlatNumber.ToLower().Contains(term)).ToList();
        }

        if (statuses is { Count: > 0 })
        {
            all = all.Where(f => statuses.Contains(f.Status)).ToList();
        }

        return all;
    }

    public async Task<PaginatedResult<FlatContributionDto>> Handle(GetFlatContributionsQuery request, CancellationToken ct)
    {
        var all = await BuildFlatContributionsAsync(request.FestivalId, ct);
        all = ApplyFilters(all, request.Search, request.Statuses);

        all = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("target", false) => all.OrderBy(f => f.TargetAmount).ToList(),
            ("target", true) => all.OrderByDescending(f => f.TargetAmount).ToList(),
            ("paid", false) => all.OrderBy(f => f.PaidAmount).ToList(),
            ("paid", true) => all.OrderByDescending(f => f.PaidAmount).ToList(),
            ("outstanding", false) => all.OrderBy(f => f.OutstandingAmount).ToList(),
            ("outstanding", true) => all.OrderByDescending(f => f.OutstandingAmount).ToList(),
            ("status", false) => all.OrderBy(f => f.Status).ToList(),
            ("status", true) => all.OrderByDescending(f => f.Status).ToList(),
            (_, true) => all.OrderByDescending(f => f.FlatId).ToList(),
            _ => all.OrderBy(f => f.FlatId).ToList()
        };

        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedResult<FlatContributionDto>(items, all.Count, pageNumber, pageSize);
    }

    public async Task<FlatContributionKpisDto> Handle(GetFlatContributionKpisQuery request, CancellationToken ct)
    {
        var all = await BuildFlatContributionsAsync(request.FestivalId, ct);

        return new FlatContributionKpisDto
        {
            TotalFlats = all.Count,
            TotalTargetAmount = all.Sum(f => f.TargetAmount),
            TotalPaidAmount = all.Sum(f => f.PaidAmount),
            TotalOutstandingAmount = all.Sum(f => f.OutstandingAmount),
            FlatsPaidCount = all.Count(f => f.Status == FlatContributionStatus.Paid),
            FlatsPartiallyPaidCount = all.Count(f => f.Status == FlatContributionStatus.PartiallyPaid),
            FlatsPendingCount = all.Count(f => f.Status == FlatContributionStatus.Pending),
            FlatsNoTargetCount = all.Count(f => f.Status == FlatContributionStatus.NoTarget)
        };
    }

    public async Task<FlatContributionsSumDto> Handle(GetFlatContributionsSumQuery request, CancellationToken ct)
    {
        var all = await BuildFlatContributionsAsync(request.FestivalId, ct);
        all = ApplyFilters(all, request.Search, request.Statuses);

        return new FlatContributionsSumDto
        {
            FlatCount = all.Count,
            TotalTargetAmount = all.Sum(f => f.TargetAmount),
            TotalPaidAmount = all.Sum(f => f.PaidAmount),
            TotalOutstandingAmount = all.Sum(f => f.OutstandingAmount)
        };
    }

    public async Task<List<ContributableFlatDto>> Handle(GetContributableFlatsQuery request, CancellationToken ct)
    {
        var all = await BuildFlatContributionsAsync(request.FestivalId, ct);

        return all.Where(f => f.Status != FlatContributionStatus.Paid)
            .OrderBy(f => f.FlatId)
            .Select(f => new ContributableFlatDto { FlatId = f.FlatId, FlatNumber = f.FlatNumber })
            .ToList();
    }
}
