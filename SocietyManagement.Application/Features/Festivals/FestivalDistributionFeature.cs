using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Application.Features.Occupancy;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;
using PermissionCodes = SocietyManagement.Shared.Constants.Permissions;

namespace SocietyManagement.Application.Features.Festivals;

// ---- DTOs ----------------------------------------------------------------------

/// <summary>Generic festival giveaway (Kurta, T-shirt, gift, prasad,
/// coupon...) — one card in the Distributions tab. Counts are rolled up at
/// query time from the claim rows, same "compute don't denormalize"
/// convention the rest of the Festival module already uses.</summary>
public class FestivalDistributionDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public string ItemName { get; set; } = default!;
    public string? Description { get; set; }
    public DistributionEligibilityType EligibilityType { get; set; }
    public decimal? EligibilityMinContribution { get; set; }
    public int QuantityPerFlat { get; set; }
    public FestivalDistributionStatus Status { get; set; }
    public int EligibleFlatsCount { get; set; }
    public int TotalQuantity { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int DistributedCount { get; set; }
}

public class FestivalDistributionVariantDto
{
    public int Id { get; set; }
    public string Label { get; set; } = default!;
    public int SortOrder { get; set; }
}

/// <summary>One row of the admin dashboard's size/variant-wise requirement
/// table — "Unspecified" groups slots nobody has picked a variant for yet.</summary>
public class DistributionVariantBreakdownDto
{
    public int? VariantId { get; set; }
    public string VariantLabel { get; set; } = default!;
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int DistributedCount { get; set; }
    public int TotalCount { get; set; }
}

public class FestivalDistributionDetailDto : FestivalDistributionDto
{
    public List<FestivalDistributionVariantDto> Variants { get; set; } = new();
    public List<DistributionVariantBreakdownDto> VariantBreakdown { get; set; } = new();
}

public class DistributionClaimDto
{
    public int Id { get; set; }
    public int FestivalDistributionId { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public int SlotNumber { get; set; }
    public int? MemberId { get; set; }
    public string? MemberName { get; set; }
    public int? VariantId { get; set; }
    public string? VariantLabel { get; set; }
    public DistributionClaimStatus Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DistributedAt { get; set; }
    /// <summary>Non-null only for a paid add-on slot beyond the free
    /// per-flat quota — the amount charged for this one unit.</summary>
    public decimal? Amount { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Powers the "assign to member" dropdown — every current
/// resident of the claim's own flat, sourced from the Occupancy/Person
/// household model (the actual family roster), not the older Member
/// entity. MemberId here is really a Person id — named for API stability.</summary>
public class FlatMemberOptionDto
{
    public int MemberId { get; set; }
    public string Name { get; set; } = default!;
}

// ---- Commands: Distribution setup -----------------------------------------------

public record CreateDistributionCommand(
    int FestivalId, string ItemName, string? Description, DistributionEligibilityType EligibilityType,
    decimal? EligibilityMinContribution, int QuantityPerFlat, List<string>? VariantLabels) : IRequest<int>;

public class CreateDistributionCommandValidator : AbstractValidator<CreateDistributionCommand>
{
    public CreateDistributionCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.QuantityPerFlat).GreaterThan(0);
        RuleFor(x => x.EligibilityMinContribution).NotNull().GreaterThanOrEqualTo(0)
            .When(x => x.EligibilityType == DistributionEligibilityType.MinimumAmount)
            .WithMessage("Enter the minimum contribution amount.");
    }
}

public record UpdateDistributionCommand(
    int Id, string ItemName, string? Description, DistributionEligibilityType EligibilityType,
    decimal? EligibilityMinContribution, int QuantityPerFlat) : IRequest<Unit>;

public class UpdateDistributionCommandValidator : AbstractValidator<UpdateDistributionCommand>
{
    public UpdateDistributionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.QuantityPerFlat).GreaterThan(0);
        RuleFor(x => x.EligibilityMinContribution).NotNull().GreaterThanOrEqualTo(0)
            .When(x => x.EligibilityType == DistributionEligibilityType.MinimumAmount)
            .WithMessage("Enter the minimum contribution amount.");
    }
}

public record DeleteDistributionCommand(int Id) : IRequest<Unit>;

public record AddDistributionVariantCommand(int FestivalDistributionId, string Label) : IRequest<int>;

public class AddDistributionVariantCommandValidator : AbstractValidator<AddDistributionVariantCommand>
{
    public AddDistributionVariantCommandValidator()
    {
        RuleFor(x => x.FestivalDistributionId).GreaterThan(0);
        RuleFor(x => x.Label).NotEmpty().MaximumLength(50);
    }
}

public record DeleteDistributionVariantCommand(int Id) : IRequest<Unit>;

/// <summary>Finds every eligible flat (per the distribution's own
/// EligibilityType) that doesn't already have claims for this distribution
/// and creates QuantityPerFlat Pending slots for each. Safe to re-run:
/// already-processed flats are skipped, so running it again after more
/// flats become eligible only tops up the new ones, mirroring how
/// "Generate Bills" is re-runnable month to month.</summary>
public record GenerateDistributionClaimsCommand(int FestivalDistributionId) : IRequest<int>;

/// <summary>The manual/bulk-override path — add one specific flat's slots
/// regardless of whatever the eligibility rule says (e.g. "give this flat
/// one anyway"). Tops up rather than resets if the flat already has slots:
/// new slot numbers continue from its current highest.</summary>
public record AddManualClaimCommand(int FestivalDistributionId, int FlatId, int Quantity) : IRequest<int>;

public class AddManualClaimCommandValidator : AbstractValidator<AddManualClaimCommand>
{
    public AddManualClaimCommandValidator()
    {
        RuleFor(x => x.FestivalDistributionId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

/// <summary>The "Not Qualified" action — drops a flat out of the
/// distribution entirely by removing its claim slots, for a flat that was
/// generated/added by mistake or turns out not to actually qualify. Refuses
/// if any of the flat's slots have already been handed over (Distributed),
/// since that's a real-world fact that can't be undone by a list edit.</summary>
public record RemoveFlatClaimsCommand(int FestivalDistributionId, int FlatId) : IRequest<int>;

public class RemoveFlatClaimsCommandValidator : AbstractValidator<RemoveFlatClaimsCommand>
{
    public RemoveFlatClaimsCommandValidator()
    {
        RuleFor(x => x.FestivalDistributionId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
    }
}

/// <summary>The paid-add-on path — a flat wants more than its free
/// QuantityPerFlat allocation (e.g. a 3rd Kurta for an extra family
/// member). Creates the extra claim slot(s) AND raises a real Special
/// Charge on the flat for the agreed amount, reusing the existing
/// Maintenance billing pipeline instead of inventing a parallel payment
/// concept — the charge shows up, and gets collected, exactly like any
/// other special charge.</summary>
public record AddChargeableExtraClaimCommand(
    int FestivalDistributionId, int FlatId, int Quantity, decimal AmountPerUnit, string? Notes) : IRequest<int>;

public class AddChargeableExtraClaimCommandValidator : AbstractValidator<AddChargeableExtraClaimCommand>
{
    public AddChargeableExtraClaimCommandValidator()
    {
        RuleFor(x => x.FestivalDistributionId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.AmountPerUnit).GreaterThan(0);
    }
}

/// <summary>Quick-add for a household member who isn't in the flat's
/// roster yet, without leaving the Distribution dialog. Delegates to the
/// existing Occupancy commands (AddTenantFamilyMemberCommand if the flat
/// currently has a tenant living there, otherwise AddOwnerMemberCommand) so
/// the new person becomes a real, persistent part of that flat's household
/// — not a one-off record scoped to this festival.</summary>
public record AddFlatResidentCommand(int FlatId, string FirstName, string LastName, string? Phone, PersonRelationship Relationship)
    : IRequest<FlatMemberOptionDto>;

public class AddFlatResidentCommandValidator : AbstractValidator<AddFlatResidentCommand>
{
    public AddFlatResidentCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("A valid 10-digit mobile number is required.");
    }
}

// ---- Commands: Claim lifecycle --------------------------------------------------

public record AssignClaimMemberCommand(int ClaimId, int? MemberId) : IRequest<Unit>;

/// <summary>The member-facing action — picking a variant (e.g. a Kurta
/// size) for one of their own flat's Pending slots, optionally also
/// tagging which household member it's for. The same command serves an
/// admin assisting a walk-in resident: anyone holding ManageDistribution
/// may act on any flat's claim; everyone else may only act on their own
/// flat's (enforced by resolving the caller's own Member/FlatResidency
/// rows, not just a coarse permission check).</summary>
public record SelectClaimVariantCommand(int ClaimId, int? VariantId, int? MemberId) : IRequest<Unit>;

public record MarkClaimDistributedCommand(int ClaimId) : IRequest<Unit>;

/// <summary>Bulk convenience matching the real handover moment — a family
/// picks up every item for their flat at once rather than one slot at a
/// time. Returns how many slots were actually moved to Distributed.</summary>
public record MarkFlatDistributedCommand(int FestivalDistributionId, int FlatId) : IRequest<int>;

public class DistributionCommandHandlers :
    IRequestHandler<CreateDistributionCommand, int>,
    IRequestHandler<UpdateDistributionCommand, Unit>,
    IRequestHandler<DeleteDistributionCommand, Unit>,
    IRequestHandler<AddDistributionVariantCommand, int>,
    IRequestHandler<DeleteDistributionVariantCommand, Unit>,
    IRequestHandler<GenerateDistributionClaimsCommand, int>,
    IRequestHandler<AddManualClaimCommand, int>,
    IRequestHandler<RemoveFlatClaimsCommand, int>,
    IRequestHandler<AddChargeableExtraClaimCommand, int>,
    IRequestHandler<AddFlatResidentCommand, FlatMemberOptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IMediator _mediator;

    public DistributionCommandHandlers(IApplicationDbContext context, IAuditService auditService, IMediator mediator)
    {
        _context = context;
        _auditService = auditService;
        _mediator = mediator;
    }

    public async Task<int> Handle(CreateDistributionCommand request, CancellationToken ct)
    {
        if (!await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
            throw new NotFoundException(nameof(Festival), request.FestivalId);

        var distribution = new FestivalDistribution
        {
            FestivalId = request.FestivalId, ItemName = request.ItemName, Description = request.Description,
            EligibilityType = request.EligibilityType,
            EligibilityMinContribution = request.EligibilityType == DistributionEligibilityType.MinimumAmount ? request.EligibilityMinContribution : null,
            QuantityPerFlat = request.QuantityPerFlat, Status = FestivalDistributionStatus.Draft
        };

        var sortOrder = 0;
        foreach (var label in (request.VariantLabels ?? new()).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            distribution.Variants.Add(new FestivalDistributionVariant { Label = label.Trim(), SortOrder = sortOrder++ });
        }

        await _context.FestivalDistributions.AddAsync(distribution, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalDistribution), distribution.Id.ToString(), ct: ct);
        return distribution.Id;
    }

    public async Task<Unit> Handle(UpdateDistributionCommand request, CancellationToken ct)
    {
        var distribution = await _context.FestivalDistributions.FirstOrDefaultAsync(d => d.Id == request.Id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistribution), request.Id);

        distribution.ItemName = request.ItemName;
        distribution.Description = request.Description;
        distribution.EligibilityType = request.EligibilityType;
        distribution.EligibilityMinContribution = request.EligibilityType == DistributionEligibilityType.MinimumAmount ? request.EligibilityMinContribution : null;
        distribution.QuantityPerFlat = request.QuantityPerFlat;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalDistribution), distribution.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteDistributionCommand request, CancellationToken ct)
    {
        var distribution = await _context.FestivalDistributions.FirstOrDefaultAsync(d => d.Id == request.Id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistribution), request.Id);

        if (await _context.FestivalDistributionClaims.AnyAsync(c => c.FestivalDistributionId == distribution.Id && !c.IsDeleted, ct))
            throw new ConflictAppException("Can't delete a distribution once eligibility has been generated. Claims already exist for it.");

        distribution.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalDistribution), distribution.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<int> Handle(AddDistributionVariantCommand request, CancellationToken ct)
    {
        if (!await _context.FestivalDistributions.AnyAsync(d => d.Id == request.FestivalDistributionId && !d.IsDeleted, ct))
            throw new NotFoundException(nameof(FestivalDistribution), request.FestivalDistributionId);

        var maxSortOrder = await _context.FestivalDistributionVariants
            .Where(v => v.FestivalDistributionId == request.FestivalDistributionId && !v.IsDeleted)
            .Select(v => (int?)v.SortOrder)
            .MaxAsync(ct) ?? -1;

        var variant = new FestivalDistributionVariant
        {
            FestivalDistributionId = request.FestivalDistributionId, Label = request.Label.Trim(), SortOrder = maxSortOrder + 1
        };
        await _context.FestivalDistributionVariants.AddAsync(variant, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalDistributionVariant), variant.Id.ToString(), ct: ct);
        return variant.Id;
    }

    public async Task<Unit> Handle(DeleteDistributionVariantCommand request, CancellationToken ct)
    {
        var variant = await _context.FestivalDistributionVariants.FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistributionVariant), request.Id);

        if (await _context.FestivalDistributionClaims.AnyAsync(c => c.VariantId == variant.Id && !c.IsDeleted, ct))
            throw new ConflictAppException("Can't remove a variant that's already selected on at least one claim.");

        variant.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalDistributionVariant), variant.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<int> Handle(GenerateDistributionClaimsCommand request, CancellationToken ct)
    {
        var distribution = await _context.FestivalDistributions.FirstOrDefaultAsync(d => d.Id == request.FestivalDistributionId && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistribution), request.FestivalDistributionId);

        var festival = await _context.Festivals.FirstAsync(f => f.Id == distribution.FestivalId, ct);

        var allFlatIds = await _context.Flats
            .Where(f => !f.IsDeleted && f.Floor.Wing.Building.SocietyId == festival.SocietyId)
            .Select(f => f.Id)
            .ToListAsync(ct);

        List<int> eligibleFlatIds;
        if (distribution.EligibilityType == DistributionEligibilityType.AllFlats)
        {
            eligibleFlatIds = allFlatIds;
        }
        else
        {
            // Built from the full flat list first, then looked up in a paid-
            // amount dictionary that defaults to 0 for flats with no
            // contribution rows at all — a flat that never contributed must
            // still be considered (and correctly excluded, at 0), not simply
            // absent from a GroupBy that only ever sees flats WITH rows.
            var paidByFlat = await _context.FestivalContributions
                .Where(c => c.FestivalId == festival.Id && c.FlatId != null)
                .GroupBy(c => c.FlatId!.Value)
                .Select(g => new { FlatId = g.Key, Paid = g.Sum(c => c.Amount) })
                .ToDictionaryAsync(x => x.FlatId, x => x.Paid, ct);

            eligibleFlatIds = distribution.EligibilityType == DistributionEligibilityType.MinimumAmount
                ? allFlatIds.Where(id => paidByFlat.GetValueOrDefault(id, 0m) >= (distribution.EligibilityMinContribution ?? 0m)).ToList()
                // ContributionPaid: "partially or fully paid" — anything paid at all counts,
                // same PartiallyPaid/Paid statuses the Contribution tab itself shows.
                : allFlatIds.Where(id => paidByFlat.GetValueOrDefault(id, 0m) > 0m).ToList();
        }

        var alreadyProcessedFlatIds = await _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == distribution.Id && !c.IsDeleted)
            .Select(c => c.FlatId)
            .Distinct()
            .ToListAsync(ct);

        var flatsToProcess = eligibleFlatIds.Except(alreadyProcessedFlatIds).ToList();

        var createdCount = 0;
        foreach (var flatId in flatsToProcess)
        {
            for (var slot = 1; slot <= distribution.QuantityPerFlat; slot++)
            {
                await _context.FestivalDistributionClaims.AddAsync(new FestivalDistributionClaim
                {
                    FestivalDistributionId = distribution.Id, FlatId = flatId, SlotNumber = slot,
                    Status = DistributionClaimStatus.Pending
                }, ct);
                createdCount++;
            }
        }

        if (distribution.Status == FestivalDistributionStatus.Draft && createdCount > 0)
            distribution.Status = FestivalDistributionStatus.Open;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalDistributionClaim), distribution.Id.ToString(), ct: ct);
        return createdCount;
    }

    public async Task<int> Handle(AddManualClaimCommand request, CancellationToken ct)
    {
        var distribution = await _context.FestivalDistributions.FirstOrDefaultAsync(d => d.Id == request.FestivalDistributionId && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistribution), request.FestivalDistributionId);

        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
            throw new NotFoundException(nameof(Flat), request.FlatId);

        var maxSlotNumber = await _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == distribution.Id && c.FlatId == request.FlatId && !c.IsDeleted)
            .Select(c => (int?)c.SlotNumber)
            .MaxAsync(ct) ?? 0;

        for (var i = 1; i <= request.Quantity; i++)
        {
            await _context.FestivalDistributionClaims.AddAsync(new FestivalDistributionClaim
            {
                FestivalDistributionId = distribution.Id, FlatId = request.FlatId, SlotNumber = maxSlotNumber + i,
                Status = DistributionClaimStatus.Pending
            }, ct);
        }

        if (distribution.Status == FestivalDistributionStatus.Draft)
            distribution.Status = FestivalDistributionStatus.Open;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalDistributionClaim), request.FlatId.ToString(), ct: ct);
        return request.Quantity;
    }

    public async Task<int> Handle(RemoveFlatClaimsCommand request, CancellationToken ct)
    {
        if (!await _context.FestivalDistributions.AnyAsync(d => d.Id == request.FestivalDistributionId && !d.IsDeleted, ct))
            throw new NotFoundException(nameof(FestivalDistribution), request.FestivalDistributionId);

        var claims = await _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == request.FestivalDistributionId && c.FlatId == request.FlatId && !c.IsDeleted)
            .ToListAsync(ct);

        if (claims.Count == 0)
            throw new NotFoundException(nameof(FestivalDistributionClaim), request.FlatId);

        if (claims.Any(c => c.Status == DistributionClaimStatus.Distributed))
            throw new ConflictAppException("This flat already has item(s) marked as distributed — it can't be removed from the list.");

        foreach (var claim in claims)
        {
            claim.IsDeleted = true;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalDistributionClaim), request.FlatId.ToString(), ct: ct);
        return claims.Count;
    }

    public async Task<int> Handle(AddChargeableExtraClaimCommand request, CancellationToken ct)
    {
        var distribution = await _context.FestivalDistributions.FirstOrDefaultAsync(d => d.Id == request.FestivalDistributionId && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistribution), request.FestivalDistributionId);

        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
            throw new NotFoundException(nameof(Flat), request.FlatId);

        var maxSlotNumber = await _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == distribution.Id && c.FlatId == request.FlatId && !c.IsDeleted)
            .Select(c => (int?)c.SlotNumber)
            .MaxAsync(ct) ?? 0;

        for (var i = 1; i <= request.Quantity; i++)
        {
            await _context.FestivalDistributionClaims.AddAsync(new FestivalDistributionClaim
            {
                FestivalDistributionId = distribution.Id, FlatId = request.FlatId, SlotNumber = maxSlotNumber + i,
                Status = DistributionClaimStatus.Pending, Amount = request.AmountPerUnit,
                Notes = request.Notes ?? "Chargeable extra"
            }, ct);
        }

        if (distribution.Status == FestivalDistributionStatus.Draft)
            distribution.Status = FestivalDistributionStatus.Open;

        await _context.SaveChangesAsync(ct);

        await _mediator.Send(new CreateSpecialChargeCommand(
            request.FlatId, $"{distribution.ItemName} — Extra ({request.Quantity})", request.AmountPerUnit * request.Quantity,
            ChargeFrequency.OneTime, DateTime.UtcNow.Date, null,
            request.Notes ?? $"{request.Quantity} extra {distribution.ItemName} item(s) beyond the standard festival allocation."), ct);

        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalDistributionClaim), request.FlatId.ToString(), ct: ct);
        return request.Quantity;
    }

    public async Task<FlatMemberOptionDto> Handle(AddFlatResidentCommand request, CancellationToken ct)
    {
        var tenantOccupancy = await _context.FlatOccupancies.FirstOrDefaultAsync(
            o => o.FlatId == request.FlatId && o.Type == OccupancyType.Tenant && o.EndDate == null && !o.IsDeleted, ct);

        // A rented flat's actual residents are the tenant's household, not
        // the (absent) owner's — so a tenant occupancy, if one is current,
        // takes priority over creating/using an Owner occupancy.
        var occupancyMemberId = tenantOccupancy != null
            ? await _mediator.Send(new AddTenantFamilyMemberCommand(
                tenantOccupancy.Id, null, request.FirstName, request.LastName, request.Phone, null, null, null, null, null, null, null,
                request.Relationship, DateTime.UtcNow), ct)
            : await _mediator.Send(new AddOwnerMemberCommand(
                request.FlatId, null, request.FirstName, request.LastName, request.Phone, null, null, null, null, null, null, null,
                request.Relationship, false, DateTime.UtcNow), ct);

        return await _context.OccupancyMembers
            .Where(m => m.Id == occupancyMemberId)
            .Select(m => new FlatMemberOptionDto { MemberId = m.PersonId, Name = m.Person.FirstName + " " + m.Person.LastName })
            .FirstAsync(ct);
    }
}

public class DistributionClaimCommandHandlers :
    IRequestHandler<AssignClaimMemberCommand, Unit>,
    IRequestHandler<SelectClaimVariantCommand, Unit>,
    IRequestHandler<MarkClaimDistributedCommand, Unit>,
    IRequestHandler<MarkFlatDistributedCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public DistributionClaimCommandHandlers(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    private async Task<List<int>> GetOwnFlatIdsAsync(CancellationToken ct) =>
        await _context.Members
            .Where(m => m.UserId == _currentUserService.UserId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .Where(r => !r.IsDeleted && r.MoveOutDate == null)
            .Select(r => r.FlatId)
            .ToListAsync(ct);

    private async Task EnsureCanActOnFlatAsync(int flatId, CancellationToken ct)
    {
        if (_currentUserService.HasPermission(PermissionCodes.Festivals.ManageDistribution)) return;

        var ownFlatIds = await GetOwnFlatIdsAsync(ct);
        if (!ownFlatIds.Contains(flatId))
            throw new ForbiddenAccessException("You can only act on your own flat's items.");
    }

    /// <summary>Is this Person a current member of this flat's household —
    /// Owner or Tenant occupancy, either one, as long as it's still open
    /// (EndDate/LeftDate null)?</summary>
    private Task<bool> IsCurrentResidentAsync(int personId, int flatId, CancellationToken ct) =>
        _context.OccupancyMembers.AnyAsync(m => m.PersonId == personId && m.LeftDate == null && !m.IsDeleted &&
            m.FlatOccupancy.FlatId == flatId && m.FlatOccupancy.EndDate == null && !m.FlatOccupancy.IsDeleted, ct);

    public async Task<Unit> Handle(AssignClaimMemberCommand request, CancellationToken ct)
    {
        var claim = await _context.FestivalDistributionClaims.FirstOrDefaultAsync(c => c.Id == request.ClaimId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistributionClaim), request.ClaimId);

        await EnsureCanActOnFlatAsync(claim.FlatId, ct);

        if (request.MemberId is int personId && !await IsCurrentResidentAsync(personId, claim.FlatId, ct))
        {
            throw new BadRequestAppException("That person doesn't currently live in this flat.");
        }

        claim.PersonId = request.MemberId;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalDistributionClaim), claim.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(SelectClaimVariantCommand request, CancellationToken ct)
    {
        var claim = await _context.FestivalDistributionClaims.FirstOrDefaultAsync(c => c.Id == request.ClaimId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistributionClaim), request.ClaimId);

        if (claim.Status == DistributionClaimStatus.Distributed)
            throw new ConflictAppException("This item has already been distributed and can no longer be changed.");

        await EnsureCanActOnFlatAsync(claim.FlatId, ct);

        if (request.VariantId is int variantId &&
            !await _context.FestivalDistributionVariants.AnyAsync(
                v => v.Id == variantId && v.FestivalDistributionId == claim.FestivalDistributionId && !v.IsDeleted, ct))
        {
            throw new BadRequestAppException("That size/variant doesn't belong to this distribution.");
        }
        if (request.MemberId is int personId && !await IsCurrentResidentAsync(personId, claim.FlatId, ct))
        {
            throw new BadRequestAppException("That person doesn't currently live in this flat.");
        }

        claim.VariantId = request.VariantId;
        claim.PersonId = request.MemberId;
        claim.Status = DistributionClaimStatus.Confirmed;
        claim.ConfirmedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalDistributionClaim), claim.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(MarkClaimDistributedCommand request, CancellationToken ct)
    {
        var claim = await _context.FestivalDistributionClaims.FirstOrDefaultAsync(c => c.Id == request.ClaimId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistributionClaim), request.ClaimId);

        claim.Status = DistributionClaimStatus.Distributed;
        claim.DistributedAt = DateTime.UtcNow;
        claim.DistributedByUserId = _currentUserService.UserId;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalDistributionClaim), claim.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<int> Handle(MarkFlatDistributedCommand request, CancellationToken ct)
    {
        var claims = await _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == request.FestivalDistributionId && c.FlatId == request.FlatId &&
                !c.IsDeleted && c.Status != DistributionClaimStatus.Distributed)
            .ToListAsync(ct);

        foreach (var claim in claims)
        {
            claim.Status = DistributionClaimStatus.Distributed;
            claim.DistributedAt = DateTime.UtcNow;
            claim.DistributedByUserId = _currentUserService.UserId;
        }

        if (claims.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
            await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalDistributionClaim), request.FlatId.ToString(), ct: ct);
        }
        return claims.Count;
    }
}

// ---- Queries ---------------------------------------------------------------------

public record GetDistributionsQuery(int FestivalId) : IRequest<List<FestivalDistributionDto>>;

public record GetDistributionByIdQuery(int Id) : IRequest<FestivalDistributionDetailDto>;

public record GetDistributionClaimsQuery(int FestivalDistributionId, int? FlatId, DistributionClaimStatus? Status) : IRequest<List<DistributionClaimDto>>;

/// <summary>The resident self-service view — every claim slot across the
/// caller's own flat(s) for this one distribution, regardless of who
/// currently "owns" the slot (MemberId), since household members decide
/// among themselves who takes which slot.</summary>
public record GetMyDistributionClaimsQuery(int FestivalDistributionId) : IRequest<List<DistributionClaimDto>>;

public record GetFlatMembersForDistributionQuery(int FlatId) : IRequest<List<FlatMemberOptionDto>>;

public class DistributionQueryHandlers :
    IRequestHandler<GetDistributionsQuery, List<FestivalDistributionDto>>,
    IRequestHandler<GetDistributionByIdQuery, FestivalDistributionDetailDto>,
    IRequestHandler<GetDistributionClaimsQuery, List<DistributionClaimDto>>,
    IRequestHandler<GetMyDistributionClaimsQuery, List<DistributionClaimDto>>,
    IRequestHandler<GetFlatMembersForDistributionQuery, List<FlatMemberOptionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DistributionQueryHandlers(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private static FestivalDistributionDto ProjectSummary(FestivalDistribution d, List<FestivalDistributionClaim> claims) => new()
    {
        Id = d.Id, FestivalId = d.FestivalId, ItemName = d.ItemName, Description = d.Description,
        EligibilityType = d.EligibilityType, EligibilityMinContribution = d.EligibilityMinContribution,
        QuantityPerFlat = d.QuantityPerFlat, Status = d.Status,
        EligibleFlatsCount = claims.Select(c => c.FlatId).Distinct().Count(),
        TotalQuantity = claims.Count,
        PendingCount = claims.Count(c => c.Status == DistributionClaimStatus.Pending),
        ConfirmedCount = claims.Count(c => c.Status == DistributionClaimStatus.Confirmed),
        DistributedCount = claims.Count(c => c.Status == DistributionClaimStatus.Distributed)
    };

    public async Task<List<FestivalDistributionDto>> Handle(GetDistributionsQuery request, CancellationToken ct)
    {
        var distributions = await _context.FestivalDistributions
            .Where(d => d.FestivalId == request.FestivalId && !d.IsDeleted)
            .ToListAsync(ct);

        var distributionIds = distributions.Select(d => d.Id).ToList();
        var claims = await _context.FestivalDistributionClaims
            .Where(c => distributionIds.Contains(c.FestivalDistributionId) && !c.IsDeleted)
            .ToListAsync(ct);

        return distributions
            .Select(d => ProjectSummary(d, claims.Where(c => c.FestivalDistributionId == d.Id).ToList()))
            .ToList();
    }

    public async Task<FestivalDistributionDetailDto> Handle(GetDistributionByIdQuery request, CancellationToken ct)
    {
        var distribution = await _context.FestivalDistributions
            .Include(d => d.Variants.Where(v => !v.IsDeleted))
            .FirstOrDefaultAsync(d => d.Id == request.Id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalDistribution), request.Id);

        var claims = await _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == distribution.Id && !c.IsDeleted)
            .ToListAsync(ct);

        var summary = ProjectSummary(distribution, claims);

        var variantGroups = claims
            .GroupBy(c => c.VariantId)
            .Select(g => new DistributionVariantBreakdownDto
            {
                VariantId = g.Key,
                VariantLabel = g.Key is int vId ? distribution.Variants.FirstOrDefault(v => v.Id == vId)?.Label ?? "—" : "Unspecified",
                PendingCount = g.Count(c => c.Status == DistributionClaimStatus.Pending),
                ConfirmedCount = g.Count(c => c.Status == DistributionClaimStatus.Confirmed),
                DistributedCount = g.Count(c => c.Status == DistributionClaimStatus.Distributed),
                TotalCount = g.Count()
            })
            .OrderBy(v => v.VariantId is null ? int.MaxValue : distribution.Variants.FirstOrDefault(x => x.Id == v.VariantId)!.SortOrder)
            .ToList();

        return new FestivalDistributionDetailDto
        {
            Id = summary.Id, FestivalId = summary.FestivalId, ItemName = summary.ItemName, Description = summary.Description,
            EligibilityType = summary.EligibilityType, EligibilityMinContribution = summary.EligibilityMinContribution,
            QuantityPerFlat = summary.QuantityPerFlat, Status = summary.Status,
            EligibleFlatsCount = summary.EligibleFlatsCount, TotalQuantity = summary.TotalQuantity,
            PendingCount = summary.PendingCount, ConfirmedCount = summary.ConfirmedCount, DistributedCount = summary.DistributedCount,
            Variants = distribution.Variants.OrderBy(v => v.SortOrder)
                .Select(v => new FestivalDistributionVariantDto { Id = v.Id, Label = v.Label, SortOrder = v.SortOrder }).ToList(),
            VariantBreakdown = variantGroups
        };
    }

    private async Task<List<DistributionClaimDto>> ProjectClaimsAsync(IQueryable<FestivalDistributionClaim> query, CancellationToken ct)
    {
        var claims = await query
            .Select(c => new
            {
                c.Id, c.FestivalDistributionId, c.FlatId, FlatNumber = c.Flat.FlatNumber, c.SlotNumber,
                c.PersonId, MemberName = c.Person == null ? null : c.Person.FirstName + " " + c.Person.LastName,
                c.VariantId, VariantLabel = c.Variant == null ? null : c.Variant.Label,
                c.Status, c.ConfirmedAt, c.DistributedAt, c.Amount, c.Notes
            })
            .OrderBy(c => c.FlatNumber).ThenBy(c => c.SlotNumber)
            .ToListAsync(ct);

        return claims.Select(c => new DistributionClaimDto
        {
            Id = c.Id, FestivalDistributionId = c.FestivalDistributionId, FlatId = c.FlatId, FlatNumber = c.FlatNumber,
            SlotNumber = c.SlotNumber, MemberId = c.PersonId, MemberName = c.MemberName, VariantId = c.VariantId,
            VariantLabel = c.VariantLabel, Status = c.Status, ConfirmedAt = c.ConfirmedAt, DistributedAt = c.DistributedAt,
            Amount = c.Amount, Notes = c.Notes
        }).ToList();
    }

    public Task<List<DistributionClaimDto>> Handle(GetDistributionClaimsQuery request, CancellationToken ct)
    {
        var query = _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == request.FestivalDistributionId && !c.IsDeleted);

        if (request.FlatId.HasValue) query = query.Where(c => c.FlatId == request.FlatId);
        if (request.Status.HasValue) query = query.Where(c => c.Status == request.Status);

        return ProjectClaimsAsync(query, ct);
    }

    public async Task<List<DistributionClaimDto>> Handle(GetMyDistributionClaimsQuery request, CancellationToken ct)
    {
        var myFlatIds = await _context.Members
            .Where(m => m.UserId == _currentUserService.UserId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .Where(r => !r.IsDeleted && r.MoveOutDate == null)
            .Select(r => r.FlatId)
            .ToListAsync(ct);

        if (myFlatIds.Count == 0) return new List<DistributionClaimDto>();

        var query = _context.FestivalDistributionClaims
            .Where(c => c.FestivalDistributionId == request.FestivalDistributionId && !c.IsDeleted && myFlatIds.Contains(c.FlatId));

        return await ProjectClaimsAsync(query, ct);
    }

    public async Task<List<FlatMemberOptionDto>> Handle(GetFlatMembersForDistributionQuery request, CancellationToken ct) =>
        await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate == null &&
                m.FlatOccupancy.FlatId == request.FlatId && !m.FlatOccupancy.IsDeleted && m.FlatOccupancy.EndDate == null)
            .Select(m => new FlatMemberOptionDto { MemberId = m.PersonId, Name = m.Person.FirstName + " " + m.Person.LastName })
            .Distinct()
            .ToListAsync(ct);
}
