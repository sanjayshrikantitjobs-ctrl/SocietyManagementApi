using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Helpers;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Flats;

public class FlatDto
{
    public int Id { get; set; }
    public int FloorId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public FlatType FlatType { get; set; }
    public decimal? AreaSqFt { get; set; }
    public FlatStatus Status { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerPhone { get; set; }
    public string? OwnerEmail { get; set; }
}

public record CreateFlatCommand(
    int FloorId, string FlatNumber, FlatType FlatType, decimal? AreaSqFt,
    string? OwnerName, string? OwnerPhone, string? OwnerEmail) : IRequest<int>;
public class CreateFlatCommandValidator : AbstractValidator<CreateFlatCommand>
{
    public CreateFlatCommandValidator()
    {
        RuleFor(x => x.FloorId).GreaterThan(0);
        RuleFor(x => x.FlatNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.OwnerPhone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.OwnerPhone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.OwnerEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.OwnerEmail));
    }
}

public record UpdateFlatCommand(
    int Id, string FlatNumber, FlatType FlatType, decimal? AreaSqFt, FlatStatus Status,
    string? OwnerName, string? OwnerPhone, string? OwnerEmail) : IRequest<Unit>;
public class UpdateFlatCommandValidator : AbstractValidator<UpdateFlatCommand>
{
    public UpdateFlatCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FlatNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.OwnerPhone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.OwnerPhone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.OwnerEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.OwnerEmail));
    }
}

public record DeleteFlatCommand(int Id) : IRequest<Unit>;

public class FlatCommandHandlers :
    IRequestHandler<CreateFlatCommand, int>,
    IRequestHandler<UpdateFlatCommand, Unit>,
    IRequestHandler<DeleteFlatCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FlatCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateFlatCommand request, CancellationToken ct)
    {
        if (!await _context.Floors.AnyAsync(f => f.Id == request.FloorId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Floor), request.FloorId);
        }
        if (await _context.Flats.AnyAsync(
                f => f.FloorId == request.FloorId && f.FlatNumber == request.FlatNumber && !f.IsDeleted, ct))
        {
            throw new ConflictAppException("A flat with this number already exists on this floor.");
        }

        var flat = new Flat
        {
            FloorId = request.FloorId, FlatNumber = request.FlatNumber, FlatType = request.FlatType,
            AreaSqFt = request.AreaSqFt, Status = FlatStatus.Vacant,
            OwnerName = request.OwnerName, OwnerPhone = request.OwnerPhone, OwnerEmail = request.OwnerEmail
        };
        await _context.Flats.AddAsync(flat, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Society", nameof(Flat), flat.Id.ToString(), ct: ct);
        return flat.Id;
    }

    public async Task<Unit> Handle(UpdateFlatCommand request, CancellationToken ct)
    {
        var flat = await _context.Flats.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Flat), request.Id);
        flat.FlatNumber = request.FlatNumber;
        flat.FlatType = request.FlatType;
        flat.AreaSqFt = request.AreaSqFt;
        flat.Status = request.Status;
        flat.OwnerName = request.OwnerName;
        flat.OwnerPhone = request.OwnerPhone;
        flat.OwnerEmail = request.OwnerEmail;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Society", nameof(Flat), flat.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteFlatCommand request, CancellationToken ct)
    {
        var flat = await _context.Flats.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Flat), request.Id);
        if (await _context.FlatResidencies.AnyAsync(r => r.FlatId == flat.Id && !r.IsDeleted && r.MoveOutDate == null, ct))
        {
            throw new ConflictAppException("Cannot delete a flat that has current residents. End their residency first.");
        }
        flat.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Society", nameof(Flat), flat.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

/// <summary><see cref="SocietyId"/> is optional only for backward
/// compatibility with existing FloorId-scoped callers (a FloorId already
/// implies one society) — any NEW caller that omits FloorId (e.g. "every
/// flat in my society") must supply it, since without either filter this
/// previously returned every flat across every society with no tenant
/// boundary at all.</summary>
public record GetFlatsQuery(int? FloorId, FlatStatus? Status, string? Search, int? SocietyId = null, int PageNumber = 1,
    int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FlatDto>>;
public record GetFlatByIdQuery(int Id) : IRequest<FlatDto>;

/// <summary>Every flat the current user currently resides at (via their own
/// Member/FlatResidency rows) — never a client-supplied filter, so this is
/// safe to expose to any authenticated resident. A person can legitimately
/// own more than one flat, hence a list rather than a single result — see
/// My Bills / My Water Tanker, which both use this instead of showing every
/// flat in the society.</summary>
public record GetMyFlatsQuery : IRequest<List<FlatDto>>;

public class FlatQueryHandlers :
    IRequestHandler<GetFlatsQuery, PaginatedResult<FlatDto>>,
    IRequestHandler<GetFlatByIdQuery, FlatDto>,
    IRequestHandler<GetMyFlatsQuery, List<FlatDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public FlatQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    /// <summary>Two parallel resident models can both back a login (see
    /// User.MemberId/User.PersonId doc comments) — a login created through
    /// the newer Occupancy/Person flow has no Member row at all, so
    /// querying Members alone silently returns zero flats for it. Unions
    /// both paths and de-dupes by flat id.</summary>
    public async Task<List<FlatDto>> Handle(GetMyFlatsQuery request, CancellationToken ct)
    {
        var flatIds = await _context.GetCurrentResidentFlatIdsAsync(_currentUserService.UserId, ct);
        if (flatIds.Count == 0) return new List<FlatDto>();

        return await _context.Flats
            .Where(f => flatIds.Contains(f.Id))
            .OrderBy(f => f.Id)
            .Select(f => new FlatDto
            {
                Id = f.Id, FloorId = f.FloorId, FlatNumber = f.FlatNumber, FlatType = f.FlatType,
                AreaSqFt = f.AreaSqFt, Status = f.Status,
                OwnerName = f.OwnerName, OwnerPhone = f.OwnerPhone, OwnerEmail = f.OwnerEmail
            })
            .ToListAsync(ct);
    }

    public async Task<PaginatedResult<FlatDto>> Handle(GetFlatsQuery request, CancellationToken ct)
    {
        var query = _context.Flats.Where(f => !f.IsDeleted);

        if (request.FloorId.HasValue)
        {
            query = query.Where(f => f.FloorId == request.FloorId);
        }
        if (request.SocietyId.HasValue)
        {
            query = query.Where(f => f.Floor.Wing.Building.SocietyId == request.SocietyId);
        }
        if (request.Status.HasValue)
        {
            query = query.Where(f => f.Status == request.Status);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(f => f.FlatNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query.OrderBy(f => f.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(f => new FlatDto
            {
                Id = f.Id, FloorId = f.FloorId, FlatNumber = f.FlatNumber, FlatType = f.FlatType,
                AreaSqFt = f.AreaSqFt, Status = f.Status,
                OwnerName = f.OwnerName, OwnerPhone = f.OwnerPhone, OwnerEmail = f.OwnerEmail
            })
            .ToListAsync(ct);

        return new PaginatedResult<FlatDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<FlatDto> Handle(GetFlatByIdQuery request, CancellationToken ct) =>
        await _context.Flats.Where(f => f.Id == request.Id && !f.IsDeleted)
            .Select(f => new FlatDto
            {
                Id = f.Id, FloorId = f.FloorId, FlatNumber = f.FlatNumber, FlatType = f.FlatType,
                AreaSqFt = f.AreaSqFt, Status = f.Status,
                OwnerName = f.OwnerName, OwnerPhone = f.OwnerPhone, OwnerEmail = f.OwnerEmail
            })
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Flat), request.Id);
}
