using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalContributionDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public int? FlatId { get; set; }
    public string? FlatNumber { get; set; }
    public string MemberName { get; set; } = default!;
    public decimal Amount { get; set; }
    public ContributionPaymentMethod PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? TransactionId { get; set; }
    public string ReceiptNumber { get; set; } = default!;
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TopContributorDto
{
    public string MemberName { get; set; } = default!;
    public string? FlatNumber { get; set; }
    public int? FlatId { get; set; }
    public decimal TotalAmount { get; set; }
    public int ContributionCount { get; set; }
}

public class PendingContributorDto
{
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
}

// ---- Commands ----------------------------------------------------------------
public record CreateContributionCommand(
    int FestivalId, int? FlatId, string MemberName, decimal Amount, ContributionPaymentMethod PaymentMethod,
    DateTime PaymentDate, string? TransactionId, bool IsAnonymous) : IRequest<int>;

public class CreateContributionCommandValidator : AbstractValidator<CreateContributionCommand>
{
    public CreateContributionCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.MemberName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.TransactionId).MaximumLength(100);
    }
}

public class ContributionCommandHandlers : IRequestHandler<CreateContributionCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public ContributionCommandHandlers(
        IApplicationDbContext context, IAuditService auditService, INotificationService notificationService)
    {
        _context = context;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public async Task<int> Handle(CreateContributionCommand request, CancellationToken ct)
    {
        if (!await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.FestivalId);
        }

        if (request.FlatId.HasValue &&
            !await _context.Flats.AnyAsync(fl => fl.Id == request.FlatId && !fl.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId.Value);
        }

        var contribution = new FestivalContribution
        {
            FestivalId = request.FestivalId,
            FlatId = request.FlatId,
            MemberName = request.IsAnonymous ? "Anonymous Donor" : request.MemberName,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = request.PaymentDate,
            TransactionId = request.TransactionId,
            IsAnonymous = request.IsAnonymous,
            ReceiptNumber = $"RCPT{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"
        };
        await _context.FestivalContributions.AddAsync(contribution, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Payment, "Festivals", nameof(FestivalContribution), contribution.Id.ToString(), ct: ct);

        var totalCollected = await _context.FestivalContributions
            .Where(c => c.FestivalId == request.FestivalId && !c.IsDeleted)
            .SumAsync(c => c.Amount, ct);
        await _notificationService.SendToAllAsync("FestivalContributionRecorded",
            new { festivalId = request.FestivalId, amount = contribution.Amount, totalCollected }, ct);

        return contribution.Id;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetContributionsQuery(
    int FestivalId, string? Search, ContributionPaymentMethod? PaymentMethod,
    string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FestivalContributionDto>>;

public record GetTopContributorsQuery(int FestivalId, int Top = 10) : IRequest<List<TopContributorDto>>;

public record GetPendingContributorsQuery(int FestivalId) : IRequest<List<PendingContributorDto>>;

public record GetContributionReceiptPdfQuery(int Id) : IRequest<byte[]>;

public class ContributionQueryHandlers :
    IRequestHandler<GetContributionsQuery, PaginatedResult<FestivalContributionDto>>,
    IRequestHandler<GetTopContributorsQuery, List<TopContributorDto>>,
    IRequestHandler<GetPendingContributorsQuery, List<PendingContributorDto>>,
    IRequestHandler<GetContributionReceiptPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly IPdfReceiptService _pdfReceiptService;

    public ContributionQueryHandlers(IApplicationDbContext context, IPdfReceiptService pdfReceiptService)
    {
        _context = context;
        _pdfReceiptService = pdfReceiptService;
    }

    public async Task<PaginatedResult<FestivalContributionDto>> Handle(GetContributionsQuery request, CancellationToken ct)
    {
        var query = _context.FestivalContributions.Where(c => c.FestivalId == request.FestivalId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.MemberName.ToLower().Contains(term) || c.ReceiptNumber.ToLower().Contains(term));
        }

        if (request.PaymentMethod.HasValue) query = query.Where(c => c.PaymentMethod == request.PaymentMethod);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("donor", false) => query.OrderBy(c => c.MemberName),
            ("donor", true) => query.OrderByDescending(c => c.MemberName),
            ("amount", false) => query.OrderBy(c => c.Amount),
            ("amount", true) => query.OrderByDescending(c => c.Amount),
            ("method", false) => query.OrderBy(c => c.PaymentMethod),
            ("method", true) => query.OrderByDescending(c => c.PaymentMethod),
            ("date", false) => query.OrderBy(c => c.PaymentDate),
            ("date", true) => query.OrderByDescending(c => c.PaymentDate),
            ("receipt", false) => query.OrderBy(c => c.ReceiptNumber),
            ("receipt", true) => query.OrderByDescending(c => c.ReceiptNumber),
            // Default and "flat": guest/no-flat contributions (FlatId null) sort last.
            _ => query.OrderBy(c => c.FlatId == null).ThenBy(c => c.Flat!.FlatNumber)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new FestivalContributionDto
            {
                Id = c.Id,
                FestivalId = c.FestivalId,
                FlatId = c.FlatId,
                FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null,
                MemberName = c.MemberName,
                Amount = c.Amount,
                PaymentMethod = c.PaymentMethod,
                PaymentDate = c.PaymentDate,
                TransactionId = c.TransactionId,
                ReceiptNumber = c.ReceiptNumber,
                IsAnonymous = c.IsAnonymous,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(ct);

        return new PaginatedResult<FestivalContributionDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<List<TopContributorDto>> Handle(GetTopContributorsQuery request, CancellationToken ct) =>
        await _context.FestivalContributions
            .Where(c => c.FestivalId == request.FestivalId && !c.IsAnonymous)
            .GroupBy(c => new { c.MemberName, FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null, c.FlatId })
            .Select(g => new TopContributorDto
            {
                MemberName = g.Key.MemberName,
                FlatNumber = g.Key.FlatNumber,
                FlatId = g.Key.FlatId,
                TotalAmount = g.Sum(c => c.Amount),
                ContributionCount = g.Count()
            })
            .OrderByDescending(c => c.TotalAmount)
            .Take(request.Top)
            .ToListAsync(ct);

    public async Task<List<PendingContributorDto>> Handle(GetPendingContributorsQuery request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        var contributedFlatIds = await _context.FestivalContributions
            .Where(c => c.FestivalId == request.FestivalId && c.FlatId != null)
            .Select(c => c.FlatId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return await _context.Flats
            .Where(fl => fl.Floor.Wing.Building.SocietyId == festival.SocietyId && !contributedFlatIds.Contains(fl.Id))
            .Select(fl => new PendingContributorDto { FlatId = fl.Id, FlatNumber = fl.FlatNumber })
            .OrderBy(fl => fl.FlatId)
            .ToListAsync(ct);
    }

    public async Task<byte[]> Handle(GetContributionReceiptPdfQuery request, CancellationToken ct)
    {
        var data = await _context.FestivalContributions
            .Where(c => c.Id == request.Id)
            .Select(c => new
            {
                c.ReceiptNumber,
                SocietyName = c.Festival.Society.Name,
                SocietyLogoUrl = c.Festival.Society.LogoUrl,
                FestivalId = c.FestivalId,
                FestivalName = c.Festival.Name,
                c.MemberName,
                c.FlatId,
                FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null,
                c.Amount,
                c.PaymentMethod,
                c.PaymentDate,
                c.TransactionId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(FestivalContribution), request.Id);

        decimal? targetAmount = null;
        var totalPaidForFlat = data.Amount;
        if (data.FlatId.HasValue)
        {
            targetAmount = await _context.FestivalFlatTargets
                .Where(t => t.FestivalId == data.FestivalId && t.FlatId == data.FlatId)
                .Select(t => (decimal?)t.TargetAmount)
                .FirstOrDefaultAsync(ct);

            totalPaidForFlat = await _context.FestivalContributions
                .Where(c => c.FestivalId == data.FestivalId && c.FlatId == data.FlatId)
                .SumAsync(c => c.Amount, ct);
        }

        return _pdfReceiptService.GenerateContributionReceipt(new ContributionReceiptData(
            data.ReceiptNumber, data.SocietyName, data.SocietyLogoUrl, data.FestivalName, data.MemberName, data.FlatNumber,
            data.Amount, data.PaymentMethod.ToString(), data.PaymentDate, data.TransactionId,
            targetAmount, totalPaidForFlat));
    }
}
