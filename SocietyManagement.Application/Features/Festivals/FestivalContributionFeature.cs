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

        var items = await query
            .OrderByDescending(c => c.PaymentDate)
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
            .GroupBy(c => new { c.MemberName, FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null })
            .Select(g => new TopContributorDto
            {
                MemberName = g.Key.MemberName,
                FlatNumber = g.Key.FlatNumber,
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
            .OrderBy(fl => fl.FlatNumber)
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
                FestivalName = c.Festival.Name,
                c.MemberName,
                FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null,
                c.Amount,
                c.PaymentMethod,
                c.PaymentDate,
                c.TransactionId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(FestivalContribution), request.Id);

        return _pdfReceiptService.GenerateContributionReceipt(new ContributionReceiptData(
            data.ReceiptNumber, data.SocietyName, data.FestivalName, data.MemberName, data.FlatNumber,
            data.Amount, data.PaymentMethod.ToString(), data.PaymentDate, data.TransactionId));
    }
}
