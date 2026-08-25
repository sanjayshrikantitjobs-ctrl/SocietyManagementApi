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

/// <summary>List/detail shape for a Festival. Financial summary fields are
/// computed from BudgetCategories/Contributions/Expenses/Sponsors on every
/// read — nothing here is denormalized, so it can never drift.</summary>
public class FestivalDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? CoverPhotoUrl { get; set; }
    public string? Theme { get; set; }
    public FestivalStatus Status { get; set; }
    public FestivalVisibility Visibility { get; set; }
    public bool IsRecurring { get; set; }
    public int? ParentFestivalId { get; set; }
    public FestivalKind Kind { get; set; }
    public int? ContributionPoolFestivalId { get; set; }
    public string? ContributionPoolFestivalName { get; set; }

    public decimal TotalBudget { get; set; }
    public decimal Collected { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining => TotalBudget - Spent;
    public int SponsorCount { get; set; }
    public int PendingExpenseCount { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateFestivalCommand(
    int SocietyId, string Name, int Year, DateTime StartDate, DateTime EndDate,
    string? Description, string? BannerImageUrl, string? CoverPhotoUrl, string? Theme,
    FestivalVisibility Visibility, bool IsRecurring, int? ParentFestivalId,
    FestivalKind Kind = FestivalKind.Standalone, int? ContributionPoolFestivalId = null) : IRequest<int>;

public class CreateFestivalCommandValidator : AbstractValidator<CreateFestivalCommand>
{
    public CreateFestivalCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.Theme).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ContributionPoolFestivalId).Null().When(x => x.Kind != FestivalKind.Child)
            .WithMessage("Only a Child festival can be linked to a contribution pool.");
    }
}

public record UpdateFestivalCommand(
    int Id, string Name, int Year, DateTime StartDate, DateTime EndDate,
    string? Description, string? BannerImageUrl, string? CoverPhotoUrl, string? Theme,
    FestivalVisibility Visibility, bool IsRecurring,
    FestivalKind Kind = FestivalKind.Standalone, int? ContributionPoolFestivalId = null) : IRequest<Unit>;

public class UpdateFestivalCommandValidator : AbstractValidator<UpdateFestivalCommand>
{
    public UpdateFestivalCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.ContributionPoolFestivalId).Null().When(x => x.Kind != FestivalKind.Child)
            .WithMessage("Only a Child festival can be linked to a contribution pool.");
    }
}

public record UpdateFestivalStatusCommand(int Id, FestivalStatus Status) : IRequest<Unit>;

public record DeleteFestivalCommand(int Id) : IRequest<Unit>;

public class FestivalCommandHandlers :
    IRequestHandler<CreateFestivalCommand, int>,
    IRequestHandler<UpdateFestivalCommand, Unit>,
    IRequestHandler<UpdateFestivalStatusCommand, Unit>,
    IRequestHandler<DeleteFestivalCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FestivalCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateFestivalCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        if (request.ParentFestivalId.HasValue &&
            !await _context.Festivals.AnyAsync(f => f.Id == request.ParentFestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.ParentFestivalId.Value);
        }

        if (request.ContributionPoolFestivalId.HasValue)
        {
            var pool = await _context.Festivals.FirstOrDefaultAsync(
                f => f.Id == request.ContributionPoolFestivalId && !f.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Festival), request.ContributionPoolFestivalId.Value);

            if (pool.Kind != FestivalKind.Pool)
            {
                throw new ConflictAppException("The linked festival must be a Contribution Pool.");
            }
            if (pool.SocietyId != request.SocietyId)
            {
                throw new ConflictAppException("The contribution pool must belong to the same society.");
            }
        }

        var festival = new Festival
        {
            SocietyId = request.SocietyId,
            Name = request.Name,
            Year = request.Year,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Description = request.Description,
            BannerImageUrl = request.BannerImageUrl,
            CoverPhotoUrl = request.CoverPhotoUrl,
            Theme = request.Theme,
            Visibility = request.Visibility,
            IsRecurring = request.IsRecurring,
            ParentFestivalId = request.ParentFestivalId,
            Kind = request.Kind,
            ContributionPoolFestivalId = request.ContributionPoolFestivalId,
            Status = FestivalStatus.Planning
        };
        await _context.Festivals.AddAsync(festival, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(Festival), festival.Id.ToString(), ct: ct);
        return festival.Id;
    }

    public async Task<Unit> Handle(UpdateFestivalCommand request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.Id);

        if (request.ContributionPoolFestivalId.HasValue)
        {
            if (request.ContributionPoolFestivalId == festival.Id)
            {
                throw new ConflictAppException("A festival cannot be linked to itself as a contribution pool.");
            }
            var pool = await _context.Festivals.FirstOrDefaultAsync(
                f => f.Id == request.ContributionPoolFestivalId && !f.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Festival), request.ContributionPoolFestivalId.Value);

            if (pool.Kind != FestivalKind.Pool)
            {
                throw new ConflictAppException("The linked festival must be a Contribution Pool.");
            }
            if (pool.SocietyId != festival.SocietyId)
            {
                throw new ConflictAppException("The contribution pool must belong to the same society.");
            }
        }

        festival.Name = request.Name;
        festival.Year = request.Year;
        festival.StartDate = request.StartDate;
        festival.EndDate = request.EndDate;
        festival.Description = request.Description;
        festival.BannerImageUrl = request.BannerImageUrl;
        festival.CoverPhotoUrl = request.CoverPhotoUrl;
        festival.Theme = request.Theme;
        festival.Visibility = request.Visibility;
        festival.IsRecurring = request.IsRecurring;
        festival.Kind = request.Kind;
        festival.ContributionPoolFestivalId = request.ContributionPoolFestivalId;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(Festival), festival.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(UpdateFestivalStatusCommand request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.Id);

        festival.Status = request.Status;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(Festival), festival.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteFestivalCommand request, CancellationToken ct)
    {
        var festival = await _context.Festivals
            .Include(f => f.Contributions)
            .Include(f => f.Expenses)
            .Include(f => f.FlatTargets)
            .FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.Id);

        if (festival.Contributions.Any(c => !c.IsDeleted) || festival.Expenses.Any(e => !e.IsDeleted)
            || festival.FlatTargets.Any(t => !t.IsDeleted))
        {
            throw new ConflictAppException(
                "Cannot delete a festival with recorded contributions, expenses or contribution targets. Mark it Completed instead.");
        }

        festival.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(Festival), festival.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetFestivalsQuery(
    int SocietyId, FestivalStatus? Status, int? Year,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FestivalDto>>;

public record GetFestivalByIdQuery(int Id) : IRequest<FestivalDto>;

public class FestivalQueryHandlers :
    IRequestHandler<GetFestivalsQuery, PaginatedResult<FestivalDto>>,
    IRequestHandler<GetFestivalByIdQuery, FestivalDto>
{
    private readonly IApplicationDbContext _context;

    public FestivalQueryHandlers(IApplicationDbContext context) => _context = context;

    private static readonly ExpenseApprovalStatus[] SpentStatuses = { ExpenseApprovalStatus.Approved, ExpenseApprovalStatus.Paid };

    private static IQueryable<FestivalDto> Project(IQueryable<Festival> query) =>
        query.Select(f => new FestivalDto
        {
            Id = f.Id,
            SocietyId = f.SocietyId,
            Name = f.Name,
            Year = f.Year,
            StartDate = f.StartDate,
            EndDate = f.EndDate,
            Description = f.Description,
            BannerImageUrl = f.BannerImageUrl,
            CoverPhotoUrl = f.CoverPhotoUrl,
            Theme = f.Theme,
            Status = f.Status,
            Visibility = f.Visibility,
            IsRecurring = f.IsRecurring,
            ParentFestivalId = f.ParentFestivalId,
            Kind = f.Kind,
            ContributionPoolFestivalId = f.ContributionPoolFestivalId,
            ContributionPoolFestivalName = f.ContributionPoolFestival != null ? f.ContributionPoolFestival.Name : null,
            TotalBudget = f.BudgetCategories.Sum(c => (decimal?)c.ApprovedAmount) ?? 0,
            Collected = f.Contributions.Sum(c => (decimal?)c.Amount) ?? 0,
            Spent = f.Expenses.Where(e => SpentStatuses.Contains(e.ApprovalStatus)).Sum(e => (decimal?)e.Amount) ?? 0,
            SponsorCount = f.Sponsors.Count,
            PendingExpenseCount = f.Expenses.Count(e => e.ApprovalStatus == ExpenseApprovalStatus.Pending)
        });

    public async Task<PaginatedResult<FestivalDto>> Handle(GetFestivalsQuery request, CancellationToken ct)
    {
        var query = _context.Festivals.Where(f => f.SocietyId == request.SocietyId);

        if (request.Status.HasValue) query = query.Where(f => f.Status == request.Status);
        if (request.Year.HasValue) query = query.Where(f => f.Year == request.Year);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await Project(query.OrderByDescending(f => f.StartDate))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<FestivalDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<FestivalDto> Handle(GetFestivalByIdQuery request, CancellationToken ct) =>
        await Project(_context.Festivals.Where(f => f.Id == request.Id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Festival), request.Id);
}
