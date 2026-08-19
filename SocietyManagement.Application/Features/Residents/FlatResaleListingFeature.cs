using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Residents;

public class FlatResaleListingDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public string BuildingName { get; set; } = default!;
    public int ListedByMemberId { get; set; }
    public string ListedByMemberName { get; set; } = default!;
    public decimal AskingPrice { get; set; }
    public DateTime AvailableFrom { get; set; }
    public string? VisitTimingNotes { get; set; }
    public ListingStatus Status { get; set; }
    public bool NocRequested { get; set; }
    public DateTime? NocIssuedDate { get; set; }
    public string? NocDocumentUrl { get; set; }
    public string? Notes { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateListingCommand(
    int FlatId, int ListedByMemberId, decimal AskingPrice, DateTime AvailableFrom,
    string? VisitTimingNotes, bool NocRequested, string? Notes) : IRequest<int>;

public class CreateListingCommandValidator : AbstractValidator<CreateListingCommand>
{
    public CreateListingCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.ListedByMemberId).GreaterThan(0);
        RuleFor(x => x.AskingPrice).GreaterThan(0);
    }
}

public record UpdateListingCommand(
    int Id, decimal AskingPrice, DateTime AvailableFrom, string? VisitTimingNotes,
    bool NocRequested, string? Notes) : IRequest<Unit>;

public class UpdateListingCommandValidator : AbstractValidator<UpdateListingCommand>
{
    public UpdateListingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AskingPrice).GreaterThan(0);
    }
}

public record MarkUnderNegotiationCommand(int Id) : IRequest<Unit>;
public record MarkSoldCommand(int Id) : IRequest<Unit>;
public record WithdrawListingCommand(int Id) : IRequest<Unit>;
public record IssueNocCommand(int Id, DateTime NocIssuedDate, string NocDocumentUrl) : IRequest<Unit>;
public record DeleteListingCommand(int Id) : IRequest<Unit>;

/// <summary>Transition commands follow the same shape as
/// FestivalExpenseFeature's Submit/Approve/Reject state machine.</summary>
public class FlatResaleListingCommandHandlers :
    IRequestHandler<CreateListingCommand, int>,
    IRequestHandler<UpdateListingCommand, Unit>,
    IRequestHandler<MarkUnderNegotiationCommand, Unit>,
    IRequestHandler<MarkSoldCommand, Unit>,
    IRequestHandler<WithdrawListingCommand, Unit>,
    IRequestHandler<IssueNocCommand, Unit>,
    IRequestHandler<DeleteListingCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FlatResaleListingCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateListingCommand request, CancellationToken ct)
    {
        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }
        if (!await _context.Members.AnyAsync(m => m.Id == request.ListedByMemberId && !m.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Member), request.ListedByMemberId);
        }
        if (await _context.FlatResaleListings.AnyAsync(
            l => l.FlatId == request.FlatId && !l.IsDeleted &&
                (l.Status == ListingStatus.Available || l.Status == ListingStatus.UnderNegotiation), ct))
        {
            throw new ConflictAppException("This flat already has an active resale listing.");
        }

        var listing = new FlatResaleListing
        {
            FlatId = request.FlatId, ListedByMemberId = request.ListedByMemberId, AskingPrice = request.AskingPrice,
            AvailableFrom = request.AvailableFrom, VisitTimingNotes = request.VisitTimingNotes,
            NocRequested = request.NocRequested, Notes = request.Notes, Status = ListingStatus.Available
        };
        await _context.FlatResaleListings.AddAsync(listing, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Residents", nameof(FlatResaleListing), listing.Id.ToString(), ct: ct);
        return listing.Id;
    }

    public async Task<Unit> Handle(UpdateListingCommand request, CancellationToken ct)
    {
        var listing = await GetActiveListingAsync(request.Id, ct);
        listing.AskingPrice = request.AskingPrice;
        listing.AvailableFrom = request.AvailableFrom;
        listing.VisitTimingNotes = request.VisitTimingNotes;
        listing.NocRequested = request.NocRequested;
        listing.Notes = request.Notes;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(FlatResaleListing), listing.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(MarkUnderNegotiationCommand request, CancellationToken ct)
    {
        var listing = await GetListingAsync(request.Id, ct);
        if (listing.Status != ListingStatus.Available)
        {
            throw new ConflictAppException("Only an available listing can be marked under negotiation.");
        }
        listing.Status = ListingStatus.UnderNegotiation;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(FlatResaleListing), listing.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(MarkSoldCommand request, CancellationToken ct)
    {
        var listing = await GetListingAsync(request.Id, ct);
        if (listing.Status is ListingStatus.Sold or ListingStatus.Withdrawn)
        {
            throw new ConflictAppException("This listing is already closed.");
        }
        listing.Status = ListingStatus.Sold;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(FlatResaleListing), listing.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(WithdrawListingCommand request, CancellationToken ct)
    {
        var listing = await GetListingAsync(request.Id, ct);
        if (listing.Status is ListingStatus.Sold or ListingStatus.Withdrawn)
        {
            throw new ConflictAppException("This listing is already closed.");
        }
        listing.Status = ListingStatus.Withdrawn;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(FlatResaleListing), listing.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(IssueNocCommand request, CancellationToken ct)
    {
        var listing = await GetListingAsync(request.Id, ct);
        listing.NocIssuedDate = request.NocIssuedDate;
        listing.NocDocumentUrl = request.NocDocumentUrl;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Approve, "Residents", nameof(FlatResaleListing), listing.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteListingCommand request, CancellationToken ct)
    {
        var listing = await GetListingAsync(request.Id, ct);
        listing.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Residents", nameof(FlatResaleListing), listing.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    private async Task<FlatResaleListing> GetListingAsync(int id, CancellationToken ct) =>
        await _context.FlatResaleListings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
        ?? throw new NotFoundException(nameof(FlatResaleListing), id);

    private async Task<FlatResaleListing> GetActiveListingAsync(int id, CancellationToken ct)
    {
        var listing = await GetListingAsync(id, ct);
        if (listing.Status is ListingStatus.Sold or ListingStatus.Withdrawn)
        {
            throw new ConflictAppException("This listing is closed and can no longer be edited.");
        }
        return listing;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetListingsQuery(
    int SocietyId, ListingStatus? Status,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FlatResaleListingDto>>;

public record GetListingByIdQuery(int Id) : IRequest<FlatResaleListingDto>;

public class FlatResaleListingQueryHandlers :
    IRequestHandler<GetListingsQuery, PaginatedResult<FlatResaleListingDto>>,
    IRequestHandler<GetListingByIdQuery, FlatResaleListingDto>
{
    private readonly IApplicationDbContext _context;

    public FlatResaleListingQueryHandlers(IApplicationDbContext context) => _context = context;

    private static IQueryable<FlatResaleListingDto> Project(IQueryable<FlatResaleListing> query) =>
        query.Select(l => new FlatResaleListingDto
        {
            Id = l.Id, FlatId = l.FlatId, FlatNumber = l.Flat.FlatNumber, BuildingName = l.Flat.Floor.Wing.Building.Name,
            ListedByMemberId = l.ListedByMemberId, ListedByMemberName = l.ListedByMember.FirstName + " " + l.ListedByMember.LastName,
            AskingPrice = l.AskingPrice, AvailableFrom = l.AvailableFrom, VisitTimingNotes = l.VisitTimingNotes,
            Status = l.Status, NocRequested = l.NocRequested, NocIssuedDate = l.NocIssuedDate,
            NocDocumentUrl = l.NocDocumentUrl, Notes = l.Notes
        });

    public async Task<PaginatedResult<FlatResaleListingDto>> Handle(GetListingsQuery request, CancellationToken ct)
    {
        var query = _context.FlatResaleListings.Where(l => l.Flat.Floor.Wing.Building.SocietyId == request.SocietyId);

        if (request.Status.HasValue) query = query.Where(l => l.Status == request.Status);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await Project(query.OrderByDescending(l => l.CreatedAt))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<FlatResaleListingDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<FlatResaleListingDto> Handle(GetListingByIdQuery request, CancellationToken ct) =>
        await Project(_context.FlatResaleListings.Where(l => l.Id == request.Id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(FlatResaleListing), request.Id);
}
