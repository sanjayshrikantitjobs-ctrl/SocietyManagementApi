using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Helpers;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Assets;

public class AssetBookingItemDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AssetName { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public int QuantityIssued { get; set; }
    public int QuantityReturned { get; set; }
    public int QuantityDamaged { get; set; }
    public int QuantityLost { get; set; }
}

public class AssetBookingDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Notes { get; set; }
    public decimal RentalCharge { get; set; }
    public decimal SecurityDepositAmount { get; set; }
    public decimal DamageChargeAmount { get; set; }
    public decimal LateChargeAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositRefundAmount { get; set; }
    public AssetBookingStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public int? FacilityBookingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AssetBookingItemDto> Items { get; set; } = new();
}

// ==================== Commands ====================

public record AssetBookingItemInput(int AssetId, int Quantity);

public record CreateAssetBookingCommand(
    int FlatId, DateTime StartDate, DateTime EndDate, string? Notes, int? FacilityBookingId,
    List<AssetBookingItemInput> Items) : IRequest<int>;

public class CreateAssetBookingCommandValidator : AbstractValidator<CreateAssetBookingCommand>
{
    public CreateAssetBookingCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must be on or after the start date.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Add at least one asset to the rental request.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.AssetId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

public record UpdateAssetBookingStatusCommand(int Id, AssetBookingStatus Status, string? RejectionReason) : IRequest;

public record RecordAssetIssueCommand(int ItemId, int QuantityIssued) : IRequest;

public record RecordAssetReturnCommand(int ItemId, int QuantityReturned, int QuantityDamaged, int QuantityLost) : IRequest;

public record RecordAssetDepositRefundCommand(int Id, decimal DepositRefundAmount) : IRequest;

public class AssetBookingCommandHandlers :
    IRequestHandler<CreateAssetBookingCommand, int>,
    IRequestHandler<UpdateAssetBookingStatusCommand>,
    IRequestHandler<RecordAssetIssueCommand>,
    IRequestHandler<RecordAssetReturnCommand>,
    IRequestHandler<RecordAssetDepositRefundCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public AssetBookingCommandHandlers(IApplicationDbContext context, ICurrentUserService currentUser, IAuditService auditService)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateAssetBookingCommand request, CancellationToken ct)
    {
        var flat = await _context.Flats
            .Where(f => f.Id == request.FlatId && !f.IsDeleted)
            .Select(f => new { f.Id, SocietyId = f.Floor.Wing.Building.SocietyId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Flat), request.FlatId);

        if (!_currentUser.HasPermission(SocietyManagement.Shared.Constants.Permissions.Assets.Manage))
        {
            var resides = await _context.IsCurrentResidentOfFlatAsync(_currentUser.UserId, request.FlatId, ct);
            if (!resides) throw new ForbiddenAccessException("You are not a current resident of this flat.");
        }

        if (request.StartDate.Date < DateTime.UtcNow.Date) throw new BadRequestAppException("Cannot rent an asset for a past date.");

        var assetIds = request.Items.Select(i => i.AssetId).Distinct().ToList();
        var assets = await _context.Assets.Where(a => assetIds.Contains(a.Id) && a.SocietyId == flat.SocietyId).ToListAsync(ct);
        if (assets.Count != assetIds.Count) throw new NotFoundException(nameof(Asset), assetIds.First());
        if (assets.Any(a => !a.IsActive)) throw new BadRequestAppException("One or more selected assets are not currently available for rent.");

        var callerId = _currentUser.UserId!.Value;
        var callerName = await _context.Users.Where(u => u.Id == callerId)
            .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct) ?? "Unknown";

        // Duration in whole days, inclusive of both ends — the only
        // granularity residents pick for an asset rental (date range, no
        // time-of-day), so PerDay/PerHour pricing both scale by this same
        // day count; PerItem/Lumpsum charge once regardless of duration.
        var days = (request.EndDate.Date - request.StartDate.Date).Days + 1;

        // Same Serializable-transaction technique as
        // FacilityBookingFeature's overlap guard, applied to a quantity sum
        // instead of a time-range overlap — see IApplicationDbContext.
        // BeginSerializableTransactionAsync's doc comment.
        await using var transaction = await _context.BeginSerializableTransactionAsync(ct);
        try
        {
            var booking = new AssetBooking
            {
                SocietyId = flat.SocietyId, FlatId = request.FlatId, RequestedByUserId = callerId, RequestedByName = callerName,
                StartDate = request.StartDate.Date, EndDate = request.EndDate.Date, Notes = request.Notes,
                FacilityBookingId = request.FacilityBookingId, Status = AssetBookingStatus.Pending
            };

            decimal rentalCharge = 0, securityDeposit = 0;
            foreach (var input in request.Items)
            {
                var asset = assets.First(a => a.Id == input.AssetId);

                var reserved = await _context.AssetBookingItems
                    .Where(i => i.AssetId == input.AssetId &&
                                i.AssetBooking.Status != AssetBookingStatus.Rejected && i.AssetBooking.Status != AssetBookingStatus.Cancelled &&
                                i.AssetBooking.StartDate <= booking.EndDate && i.AssetBooking.EndDate >= booking.StartDate)
                    .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

                if (reserved + input.Quantity > asset.TotalQuantity)
                {
                    throw new ConflictAppException(
                        $"Only {Math.Max(asset.TotalQuantity - reserved, 0)} of \"{asset.Name}\" are available for the selected dates.");
                }

                var multiplier = asset.PricingType is AssetPricingType.PerItem or AssetPricingType.Lumpsum ? 1 : days;
                var lineTotal = Math.Round(asset.RentalPrice * input.Quantity * multiplier, 2);
                rentalCharge += lineTotal;
                securityDeposit += asset.SecurityDeposit * input.Quantity;

                booking.Items.Add(new AssetBookingItem
                {
                    AssetId = asset.Id, Quantity = input.Quantity, UnitPrice = asset.RentalPrice, LineTotal = lineTotal
                });
            }

            booking.RentalCharge = rentalCharge;
            booking.SecurityDepositAmount = securityDeposit;
            booking.TotalAmount = rentalCharge + securityDeposit;

            await _context.AssetBookings.AddAsync(booking, ct);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _auditService.LogAsync(AuditAction.Create, "AssetBookings", nameof(AssetBooking), booking.Id.ToString(),
                newValues: new { booking.FlatId, ItemCount = booking.Items.Count, booking.TotalAmount }, ct: ct);

            return booking.Id;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task Handle(UpdateAssetBookingStatusCommand request, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(request.Id, ct);
        booking.Status = request.Status;
        if (request.Status is AssetBookingStatus.Rejected or AssetBookingStatus.Cancelled)
        {
            booking.RejectionReason = request.RejectionReason;
        }
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "AssetBookings", nameof(AssetBooking), booking.Id.ToString(),
            newValues: new { Status = request.Status.ToString() }, ct: ct);
    }

    public async Task Handle(RecordAssetIssueCommand request, CancellationToken ct)
    {
        var item = await _context.AssetBookingItems.Include(i => i.AssetBooking)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new NotFoundException(nameof(AssetBookingItem), request.ItemId);
        EnsureSameSociety(item.AssetBooking.SocietyId);

        if (request.QuantityIssued > item.Quantity) throw new BadRequestAppException("Cannot issue more than the booked quantity.");
        item.QuantityIssued = request.QuantityIssued;
        await _context.SaveChangesAsync(ct);
    }

    public async Task Handle(RecordAssetReturnCommand request, CancellationToken ct)
    {
        var item = await _context.AssetBookingItems.Include(i => i.AssetBooking).ThenInclude(b => b.Items)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new NotFoundException(nameof(AssetBookingItem), request.ItemId);
        EnsureSameSociety(item.AssetBooking.SocietyId);

        var totalAccounted = request.QuantityReturned + request.QuantityDamaged + request.QuantityLost;
        if (totalAccounted > item.QuantityIssued)
        {
            throw new BadRequestAppException("Returned + damaged + lost cannot exceed the quantity issued.");
        }

        item.QuantityReturned = request.QuantityReturned;
        item.QuantityDamaged = request.QuantityDamaged;
        item.QuantityLost = request.QuantityLost;

        if (request.QuantityDamaged > 0 || request.QuantityLost > 0)
        {
            var asset = await _context.Assets.FirstAsync(a => a.Id == item.AssetId, ct);
            item.AssetBooking.DamageChargeAmount += asset.DamageCharge * (request.QuantityDamaged + request.QuantityLost);
            item.AssetBooking.TotalAmount += asset.DamageCharge * (request.QuantityDamaged + request.QuantityLost);
        }

        // EF's identity map means `item` is the same tracked instance found
        // inside AssetBooking.Items, so this already reflects the update above.
        if (item.AssetBooking.Items.All(i => i.QuantityReturned + i.QuantityDamaged + i.QuantityLost >= i.QuantityIssued))
        {
            item.AssetBooking.Status = AssetBookingStatus.Completed;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task Handle(RecordAssetDepositRefundCommand request, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(request.Id, ct);
        booking.DepositRefundAmount = request.DepositRefundAmount;
        await _context.SaveChangesAsync(ct);
    }

    private void EnsureSameSociety(int societyId)
    {
        if (_currentUser.SocietyId is { } callerSocietyId && societyId != callerSocietyId)
        {
            throw new ForbiddenAccessException("You do not have access to this society's data.");
        }
    }

    private async Task<AssetBooking> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var booking = await _context.AssetBookings.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(AssetBooking), id);
        EnsureSameSociety(booking.SocietyId);
        return booking;
    }
}

// ==================== Queries ====================

public record GetAssetBookingsQuery(
    int SocietyId, AssetBookingStatus? Status, int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize)
    : IRequest<PaginatedResult<AssetBookingDto>>;

public record GetMyAssetBookingsQuery(AssetBookingStatus? Status, int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize)
    : IRequest<PaginatedResult<AssetBookingDto>>;

public record GetAssetBookingByIdQuery(int Id) : IRequest<AssetBookingDto>;

public class AssetBookingQueryHandlers :
    IRequestHandler<GetAssetBookingsQuery, PaginatedResult<AssetBookingDto>>,
    IRequestHandler<GetMyAssetBookingsQuery, PaginatedResult<AssetBookingDto>>,
    IRequestHandler<GetAssetBookingByIdQuery, AssetBookingDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssetBookingQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private static AssetBookingDto Project(AssetBooking b) => new()
    {
        Id = b.Id, SocietyId = b.SocietyId, FlatId = b.FlatId, FlatNumber = b.Flat.FlatNumber,
        RequestedByUserId = b.RequestedByUserId, RequestedByName = b.RequestedByName, StartDate = b.StartDate, EndDate = b.EndDate,
        Notes = b.Notes, RentalCharge = b.RentalCharge, SecurityDepositAmount = b.SecurityDepositAmount,
        DamageChargeAmount = b.DamageChargeAmount, LateChargeAmount = b.LateChargeAmount, TotalAmount = b.TotalAmount,
        DepositRefundAmount = b.DepositRefundAmount, Status = b.Status, RejectionReason = b.RejectionReason,
        FacilityBookingId = b.FacilityBookingId, CreatedAt = b.CreatedAt,
        Items = b.Items.Select(i => new AssetBookingItemDto
        {
            Id = i.Id, AssetId = i.AssetId, AssetName = i.Asset.Name, Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            LineTotal = i.LineTotal, QuantityIssued = i.QuantityIssued, QuantityReturned = i.QuantityReturned,
            QuantityDamaged = i.QuantityDamaged, QuantityLost = i.QuantityLost
        }).ToList()
    };

    public async Task<PaginatedResult<AssetBookingDto>> Handle(GetAssetBookingsQuery request, CancellationToken ct)
    {
        var query = _context.AssetBookings.Include(b => b.Flat).Include(b => b.Items).ThenInclude(i => i.Asset)
            .Where(b => b.SocietyId == request.SocietyId);
        if (request.Status.HasValue) query = query.Where(b => b.Status == request.Status.Value);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = await query.OrderByDescending(b => b.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(b => Project(b)).ToListAsync(ct);

        return new PaginatedResult<AssetBookingDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PaginatedResult<AssetBookingDto>> Handle(GetMyAssetBookingsQuery request, CancellationToken ct)
    {
        var flatIds = await _context.GetCurrentResidentFlatIdsAsync(_currentUser.UserId, ct);
        var query = _context.AssetBookings.Include(b => b.Flat).Include(b => b.Items).ThenInclude(i => i.Asset)
            .Where(b => flatIds.Contains(b.FlatId));
        if (request.Status.HasValue) query = query.Where(b => b.Status == request.Status.Value);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = await query.OrderByDescending(b => b.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(b => Project(b)).ToListAsync(ct);

        return new PaginatedResult<AssetBookingDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AssetBookingDto> Handle(GetAssetBookingByIdQuery request, CancellationToken ct)
    {
        var booking = await _context.AssetBookings.Include(b => b.Flat).Include(b => b.Items).ThenInclude(i => i.Asset)
            .FirstOrDefaultAsync(b => b.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(AssetBooking), request.Id);

        if (_currentUser.SocietyId is { } callerSocietyId && booking.SocietyId != callerSocietyId)
        {
            throw new ForbiddenAccessException("You do not have access to this society's data.");
        }

        return Project(booking);
    }
}
