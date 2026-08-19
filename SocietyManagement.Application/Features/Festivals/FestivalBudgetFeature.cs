using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalBudgetCategoryDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public FestivalBudgetCategoryType Category { get; set; }
    public decimal EstimatedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Remaining => ApprovedAmount - ActualAmount;
    public string? Notes { get; set; }
}

public class FestivalBudgetRevisionDto
{
    public int Id { get; set; }
    public int FestivalBudgetCategoryId { get; set; }
    public decimal PreviousEstimatedAmount { get; set; }
    public decimal NewEstimatedAmount { get; set; }
    public decimal PreviousApprovedAmount { get; set; }
    public decimal NewApprovedAmount { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateBudgetCategoryCommand(
    int FestivalId, FestivalBudgetCategoryType Category, decimal EstimatedAmount,
    decimal ApprovedAmount, string? Notes) : IRequest<int>;

public class CreateBudgetCategoryCommandValidator : AbstractValidator<CreateBudgetCategoryCommand>
{
    public CreateBudgetCategoryCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.EstimatedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ApprovedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public record UpdateBudgetCategoryCommand(
    int Id, decimal EstimatedAmount, decimal ApprovedAmount, string? Notes, string? Reason) : IRequest<Unit>;

public class UpdateBudgetCategoryCommandValidator : AbstractValidator<UpdateBudgetCategoryCommand>
{
    public UpdateBudgetCategoryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EstimatedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ApprovedAmount).GreaterThanOrEqualTo(0);
    }
}

public record DeleteBudgetCategoryCommand(int Id) : IRequest<Unit>;

public class FestivalBudgetCommandHandlers :
    IRequestHandler<CreateBudgetCategoryCommand, int>,
    IRequestHandler<UpdateBudgetCategoryCommand, Unit>,
    IRequestHandler<DeleteBudgetCategoryCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FestivalBudgetCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateBudgetCategoryCommand request, CancellationToken ct)
    {
        if (!await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.FestivalId);
        }

        if (await _context.FestivalBudgetCategories.AnyAsync(
            c => c.FestivalId == request.FestivalId && c.Category == request.Category && !c.IsDeleted, ct))
        {
            throw new ConflictAppException($"A budget category for '{request.Category}' already exists on this festival.");
        }

        var category = new FestivalBudgetCategory
        {
            FestivalId = request.FestivalId,
            Category = request.Category,
            EstimatedAmount = request.EstimatedAmount,
            ApprovedAmount = request.ApprovedAmount,
            Notes = request.Notes
        };
        await _context.FestivalBudgetCategories.AddAsync(category, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalBudgetCategory), category.Id.ToString(), ct: ct);
        return category.Id;
    }

    public async Task<Unit> Handle(UpdateBudgetCategoryCommand request, CancellationToken ct)
    {
        var category = await _context.FestivalBudgetCategories.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalBudgetCategory), request.Id);

        if (category.EstimatedAmount != request.EstimatedAmount || category.ApprovedAmount != request.ApprovedAmount)
        {
            await _context.FestivalBudgetRevisions.AddAsync(new FestivalBudgetRevision
            {
                FestivalBudgetCategoryId = category.Id,
                PreviousEstimatedAmount = category.EstimatedAmount,
                NewEstimatedAmount = request.EstimatedAmount,
                PreviousApprovedAmount = category.ApprovedAmount,
                NewApprovedAmount = request.ApprovedAmount,
                Reason = request.Reason
            }, ct);
        }

        category.EstimatedAmount = request.EstimatedAmount;
        category.ApprovedAmount = request.ApprovedAmount;
        category.Notes = request.Notes;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalBudgetCategory), category.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteBudgetCategoryCommand request, CancellationToken ct)
    {
        var category = await _context.FestivalBudgetCategories.Include(c => c.Expenses)
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalBudgetCategory), request.Id);

        if (category.Expenses.Any(e => !e.IsDeleted))
        {
            throw new ConflictAppException("Cannot delete a budget category that has expenses recorded against it.");
        }

        category.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalBudgetCategory), category.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetBudgetCategoriesQuery(int FestivalId) : IRequest<List<FestivalBudgetCategoryDto>>;

public record GetBudgetRevisionsQuery(int FestivalBudgetCategoryId) : IRequest<List<FestivalBudgetRevisionDto>>;

public class FestivalBudgetQueryHandlers :
    IRequestHandler<GetBudgetCategoriesQuery, List<FestivalBudgetCategoryDto>>,
    IRequestHandler<GetBudgetRevisionsQuery, List<FestivalBudgetRevisionDto>>
{
    private readonly IApplicationDbContext _context;
    private static readonly ExpenseApprovalStatus[] ActualStatuses = { ExpenseApprovalStatus.Approved, ExpenseApprovalStatus.Paid };

    public FestivalBudgetQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<FestivalBudgetCategoryDto>> Handle(GetBudgetCategoriesQuery request, CancellationToken ct) =>
        await _context.FestivalBudgetCategories
            .Where(c => c.FestivalId == request.FestivalId && !c.IsDeleted)
            .Select(c => new FestivalBudgetCategoryDto
            {
                Id = c.Id,
                FestivalId = c.FestivalId,
                Category = c.Category,
                EstimatedAmount = c.EstimatedAmount,
                ApprovedAmount = c.ApprovedAmount,
                ActualAmount = c.Expenses.Where(e => ActualStatuses.Contains(e.ApprovalStatus)).Sum(e => (decimal?)e.Amount) ?? 0,
                Notes = c.Notes
            })
            .OrderBy(c => c.Category)
            .ToListAsync(ct);

    public async Task<List<FestivalBudgetRevisionDto>> Handle(GetBudgetRevisionsQuery request, CancellationToken ct) =>
        await _context.FestivalBudgetRevisions
            .Where(r => r.FestivalBudgetCategoryId == request.FestivalBudgetCategoryId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new FestivalBudgetRevisionDto
            {
                Id = r.Id,
                FestivalBudgetCategoryId = r.FestivalBudgetCategoryId,
                PreviousEstimatedAmount = r.PreviousEstimatedAmount,
                NewEstimatedAmount = r.NewEstimatedAmount,
                PreviousApprovedAmount = r.PreviousApprovedAmount,
                NewApprovedAmount = r.NewApprovedAmount,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy
            })
            .ToListAsync(ct);
}
