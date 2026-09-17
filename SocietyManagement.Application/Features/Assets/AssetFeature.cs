using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Assets;

public class AssetDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public AssetCategory Category { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int TotalQuantity { get; set; }
    public AssetPricingType PricingType { get; set; }
    public decimal RentalPrice { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal DamageCharge { get; set; }
    public decimal LateReturnCharge { get; set; }
    public bool IsActive { get; set; }
}

// ==================== Commands ====================

public record CreateAssetCommand(
    int SocietyId, string Name, AssetCategory Category, string? Description, string? ImageUrl, int TotalQuantity,
    AssetPricingType PricingType, decimal RentalPrice, decimal SecurityDeposit, decimal DamageCharge, decimal LateReturnCharge) : IRequest<int>;

public class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TotalQuantity).GreaterThan(0);
        RuleFor(x => x.RentalPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DamageCharge).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LateReturnCharge).GreaterThanOrEqualTo(0);
    }
}

public record UpdateAssetCommand(
    int Id, string Name, AssetCategory Category, string? Description, string? ImageUrl, int TotalQuantity,
    AssetPricingType PricingType, decimal RentalPrice, decimal SecurityDeposit, decimal DamageCharge,
    decimal LateReturnCharge, bool IsActive) : IRequest;

public class UpdateAssetCommandValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TotalQuantity).GreaterThan(0);
    }
}

public record DeleteAssetCommand(int Id) : IRequest;

// ==================== Queries ====================

public record GetAssetsQuery(int SocietyId, bool ActiveOnly) : IRequest<List<AssetDto>>;

public record GetAssetByIdQuery(int Id) : IRequest<AssetDto>;

/// <summary>How many units of this asset are still free across the given
/// date range — TotalQuantity minus every non-Rejected/Cancelled booking
/// item whose parent booking's [StartDate,EndDate] overlaps the range.</summary>
public record GetAssetAvailabilityQuery(int AssetId, DateTime StartDate, DateTime EndDate) : IRequest<int>;

// ==================== Handlers ====================

public class AssetCommandHandlers :
    IRequestHandler<CreateAssetCommand, int>,
    IRequestHandler<UpdateAssetCommand>,
    IRequestHandler<DeleteAssetCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public AssetCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateAssetCommand request, CancellationToken ct)
    {
        var asset = new Asset
        {
            SocietyId = request.SocietyId, Name = request.Name, Category = request.Category, Description = request.Description,
            ImageUrl = request.ImageUrl, TotalQuantity = request.TotalQuantity, PricingType = request.PricingType,
            RentalPrice = request.RentalPrice, SecurityDeposit = request.SecurityDeposit, DamageCharge = request.DamageCharge,
            LateReturnCharge = request.LateReturnCharge, IsActive = true
        };
        await _context.Assets.AddAsync(asset, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Assets", nameof(Asset), asset.Id.ToString(),
            newValues: new { asset.Name, asset.Category }, ct: ct);
        return asset.Id;
    }

    public async Task Handle(UpdateAssetCommand request, CancellationToken ct)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Asset), request.Id);

        asset.Name = request.Name;
        asset.Category = request.Category;
        asset.Description = request.Description;
        asset.ImageUrl = request.ImageUrl;
        asset.TotalQuantity = request.TotalQuantity;
        asset.PricingType = request.PricingType;
        asset.RentalPrice = request.RentalPrice;
        asset.SecurityDeposit = request.SecurityDeposit;
        asset.DamageCharge = request.DamageCharge;
        asset.LateReturnCharge = request.LateReturnCharge;
        asset.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Assets", nameof(Asset), asset.Id.ToString(), ct: ct);
    }

    public async Task Handle(DeleteAssetCommand request, CancellationToken ct)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Asset), request.Id);

        asset.IsDeleted = true;
        asset.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Assets", nameof(Asset), asset.Id.ToString(), ct: ct);
    }
}

public class AssetQueryHandlers :
    IRequestHandler<GetAssetsQuery, List<AssetDto>>,
    IRequestHandler<GetAssetByIdQuery, AssetDto>,
    IRequestHandler<GetAssetAvailabilityQuery, int>
{
    private readonly IApplicationDbContext _context;

    public AssetQueryHandlers(IApplicationDbContext context) => _context = context;

    private static AssetDto Project(Asset a) => new()
    {
        Id = a.Id, SocietyId = a.SocietyId, Name = a.Name, Category = a.Category, Description = a.Description,
        ImageUrl = a.ImageUrl, TotalQuantity = a.TotalQuantity, PricingType = a.PricingType, RentalPrice = a.RentalPrice,
        SecurityDeposit = a.SecurityDeposit, DamageCharge = a.DamageCharge, LateReturnCharge = a.LateReturnCharge, IsActive = a.IsActive
    };

    public async Task<List<AssetDto>> Handle(GetAssetsQuery request, CancellationToken ct)
    {
        var query = _context.Assets.Where(a => a.SocietyId == request.SocietyId);
        if (request.ActiveOnly) query = query.Where(a => a.IsActive);
        return await query.OrderBy(a => a.Name).Select(a => Project(a)).ToListAsync(ct);
    }

    public async Task<AssetDto> Handle(GetAssetByIdQuery request, CancellationToken ct)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Asset), request.Id);
        return Project(asset);
    }

    public async Task<int> Handle(GetAssetAvailabilityQuery request, CancellationToken ct)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, ct)
            ?? throw new NotFoundException(nameof(Asset), request.AssetId);

        var reserved = await _context.AssetBookingItems
            .Where(i => i.AssetId == request.AssetId &&
                        i.AssetBooking.Status != AssetBookingStatus.Rejected && i.AssetBooking.Status != AssetBookingStatus.Cancelled &&
                        i.AssetBooking.StartDate <= request.EndDate.Date && i.AssetBooking.EndDate >= request.StartDate.Date)
            .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        return Math.Max(asset.TotalQuantity - reserved, 0);
    }
}
