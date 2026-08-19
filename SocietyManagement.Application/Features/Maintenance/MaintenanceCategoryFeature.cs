using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Maintenance;

public class MaintenanceCategoryDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string ChargeName { get; set; } = default!;
    public ChargeType ChargeType { get; set; }
    public decimal MonthlyAmount { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateMaintenanceCategoryCommand(
    int SocietyId, string ChargeName, ChargeType ChargeType, decimal MonthlyAmount,
    DateTime EffectiveFrom, bool IsActive, int DisplayOrder) : IRequest<int>;

public class CreateMaintenanceCategoryCommandValidator : AbstractValidator<CreateMaintenanceCategoryCommand>
{
    public CreateMaintenanceCategoryCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.ChargeName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MonthlyAmount).GreaterThanOrEqualTo(0);
    }
}

public record UpdateMaintenanceCategoryCommand(
    int Id, string ChargeName, ChargeType ChargeType, decimal MonthlyAmount,
    DateTime EffectiveFrom, bool IsActive, int DisplayOrder) : IRequest<Unit>;

public class UpdateMaintenanceCategoryCommandValidator : AbstractValidator<UpdateMaintenanceCategoryCommand>
{
    public UpdateMaintenanceCategoryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ChargeName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MonthlyAmount).GreaterThanOrEqualTo(0);
    }
}

public record DeleteMaintenanceCategoryCommand(int Id) : IRequest<Unit>;

public class MaintenanceCategoryCommandHandlers :
    IRequestHandler<CreateMaintenanceCategoryCommand, int>,
    IRequestHandler<UpdateMaintenanceCategoryCommand, Unit>,
    IRequestHandler<DeleteMaintenanceCategoryCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public MaintenanceCategoryCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateMaintenanceCategoryCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var category = new MaintenanceCategory
        {
            SocietyId = request.SocietyId,
            ChargeName = request.ChargeName,
            ChargeType = request.ChargeType,
            MonthlyAmount = request.MonthlyAmount,
            EffectiveFrom = request.EffectiveFrom,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };
        await _context.MaintenanceCategories.AddAsync(category, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Maintenance", nameof(MaintenanceCategory), category.Id.ToString(), ct: ct);
        return category.Id;
    }

    public async Task<Unit> Handle(UpdateMaintenanceCategoryCommand request, CancellationToken ct)
    {
        var category = await _context.MaintenanceCategories.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(MaintenanceCategory), request.Id);

        category.ChargeName = request.ChargeName;
        category.ChargeType = request.ChargeType;
        category.MonthlyAmount = request.MonthlyAmount;
        category.EffectiveFrom = request.EffectiveFrom;
        category.IsActive = request.IsActive;
        category.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Maintenance", nameof(MaintenanceCategory), category.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteMaintenanceCategoryCommand request, CancellationToken ct)
    {
        var category = await _context.MaintenanceCategories.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(MaintenanceCategory), request.Id);

        category.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Maintenance", nameof(MaintenanceCategory), category.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetMaintenanceCategoriesQuery(int SocietyId) : IRequest<List<MaintenanceCategoryDto>>;

public class MaintenanceCategoryQueryHandlers : IRequestHandler<GetMaintenanceCategoriesQuery, List<MaintenanceCategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public MaintenanceCategoryQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<MaintenanceCategoryDto>> Handle(GetMaintenanceCategoriesQuery request, CancellationToken ct) =>
        await _context.MaintenanceCategories
            .Where(c => c.SocietyId == request.SocietyId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new MaintenanceCategoryDto
            {
                Id = c.Id, SocietyId = c.SocietyId, ChargeName = c.ChargeName, ChargeType = c.ChargeType,
                MonthlyAmount = c.MonthlyAmount, EffectiveFrom = c.EffectiveFrom, IsActive = c.IsActive,
                DisplayOrder = c.DisplayOrder
            })
            .ToListAsync(ct);
}
