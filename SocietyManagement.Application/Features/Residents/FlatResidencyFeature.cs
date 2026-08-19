using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Residents;

public class FlatResidencyDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public int MemberId { get; set; }
    public string MemberName { get; set; } = default!;
    public string MemberPhone { get; set; } = default!;
    public MemberType MemberType { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public bool IsPrimaryContact { get; set; }
    public string? Notes { get; set; }
}

/// <summary>The direct answer to "is this flat rented, by whom, contact,
/// how many people live here" — computed from FlatResidency rows, nothing
/// stored redundantly.</summary>
public class FlatOccupancySummaryDto
{
    public int FlatId { get; set; }
    public bool IsRented { get; set; }
    public FlatResidencyDto? PrimaryContact { get; set; }
    public int OccupantCount { get; set; }
    public List<FlatResidencyDto> CurrentResidents { get; set; } = new();
}

// ---- Commands ----------------------------------------------------------------
public record CreateResidencyCommand(
    int FlatId, int MemberId, MemberType MemberType, DateTime MoveInDate,
    bool IsPrimaryContact, string? Notes) : IRequest<int>;

public class CreateResidencyCommandValidator : AbstractValidator<CreateResidencyCommand>
{
    public CreateResidencyCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.MemberId).GreaterThan(0);
    }
}

public record EndResidencyCommand(int Id, DateTime MoveOutDate) : IRequest<Unit>;

public record DeleteResidencyCommand(int Id) : IRequest<Unit>;

public class FlatResidencyCommandHandlers :
    IRequestHandler<CreateResidencyCommand, int>,
    IRequestHandler<EndResidencyCommand, Unit>,
    IRequestHandler<DeleteResidencyCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FlatResidencyCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateResidencyCommand request, CancellationToken ct)
    {
        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }
        if (!await _context.Members.AnyAsync(m => m.Id == request.MemberId && !m.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Member), request.MemberId);
        }

        // A DB constraint can't express "at most one current primary contact
        // per flat", so it's enforced here.
        if (request.IsPrimaryContact && await _context.FlatResidencies.AnyAsync(
            r => r.FlatId == request.FlatId && !r.IsDeleted && r.MoveOutDate == null && r.IsPrimaryContact, ct))
        {
            throw new ConflictAppException("This flat already has a current primary contact. End their residency first.");
        }

        var residency = new FlatResidency
        {
            FlatId = request.FlatId, MemberId = request.MemberId, MemberType = request.MemberType,
            MoveInDate = request.MoveInDate, IsPrimaryContact = request.IsPrimaryContact, Notes = request.Notes
        };
        await _context.FlatResidencies.AddAsync(residency, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Residents", nameof(FlatResidency), residency.Id.ToString(), ct: ct);
        return residency.Id;
    }

    public async Task<Unit> Handle(EndResidencyCommand request, CancellationToken ct)
    {
        var residency = await _context.FlatResidencies.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FlatResidency), request.Id);

        if (residency.MoveOutDate.HasValue)
        {
            throw new ConflictAppException("This residency has already ended.");
        }

        residency.MoveOutDate = request.MoveOutDate;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(FlatResidency), residency.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteResidencyCommand request, CancellationToken ct)
    {
        var residency = await _context.FlatResidencies.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FlatResidency), request.Id);

        residency.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Residents", nameof(FlatResidency), residency.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetResidenciesQuery(int FlatId) : IRequest<List<FlatResidencyDto>>;

public record GetFlatOccupancySummaryQuery(int FlatId) : IRequest<FlatOccupancySummaryDto>;

/// <summary>All past+current Owner-type residencies for a flat, in
/// chronological order — the "history of flat owners" the spec asks for.</summary>
public record GetOwnershipHistoryQuery(int FlatId) : IRequest<List<FlatResidencyDto>>;

public class FlatResidencyQueryHandlers :
    IRequestHandler<GetResidenciesQuery, List<FlatResidencyDto>>,
    IRequestHandler<GetFlatOccupancySummaryQuery, FlatOccupancySummaryDto>,
    IRequestHandler<GetOwnershipHistoryQuery, List<FlatResidencyDto>>
{
    private readonly IApplicationDbContext _context;

    public FlatResidencyQueryHandlers(IApplicationDbContext context) => _context = context;

    private static IQueryable<FlatResidencyDto> Project(IQueryable<FlatResidency> query) =>
        query.Select(r => new FlatResidencyDto
        {
            Id = r.Id, FlatId = r.FlatId, MemberId = r.MemberId, MemberName = r.Member.FirstName + " " + r.Member.LastName,
            MemberPhone = r.Member.Phone, MemberType = r.MemberType, MoveInDate = r.MoveInDate,
            MoveOutDate = r.MoveOutDate, IsPrimaryContact = r.IsPrimaryContact, Notes = r.Notes
        });

    public async Task<List<FlatResidencyDto>> Handle(GetResidenciesQuery request, CancellationToken ct) =>
        await Project(_context.FlatResidencies.Where(r => r.FlatId == request.FlatId && !r.IsDeleted))
            .OrderByDescending(r => r.MoveInDate)
            .ToListAsync(ct);

    public async Task<FlatOccupancySummaryDto> Handle(GetFlatOccupancySummaryQuery request, CancellationToken ct)
    {
        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }

        var current = await Project(_context.FlatResidencies
                .Where(r => r.FlatId == request.FlatId && !r.IsDeleted && r.MoveOutDate == null))
            .ToListAsync(ct);

        return new FlatOccupancySummaryDto
        {
            FlatId = request.FlatId,
            IsRented = current.Any(r => r.MemberType == MemberType.Tenant),
            PrimaryContact = current.FirstOrDefault(r => r.IsPrimaryContact) ?? current.FirstOrDefault(),
            OccupantCount = current.Count,
            CurrentResidents = current
        };
    }

    public async Task<List<FlatResidencyDto>> Handle(GetOwnershipHistoryQuery request, CancellationToken ct) =>
        await Project(_context.FlatResidencies
                .Where(r => r.FlatId == request.FlatId && !r.IsDeleted && r.MemberType == MemberType.Owner))
            .OrderByDescending(r => r.MoveInDate)
            .ToListAsync(ct);
}
