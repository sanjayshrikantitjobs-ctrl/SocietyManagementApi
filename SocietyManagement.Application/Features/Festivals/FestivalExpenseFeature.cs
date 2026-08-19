using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalExpenseDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public int FestivalBudgetCategoryId { get; set; }
    public FestivalBudgetCategoryType Category { get; set; }
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public ContributionPaymentMethod PaymentMethod { get; set; }
    public string? BillImageUrl { get; set; }
    public string? InvoiceNumber { get; set; }
    public ExpenseApprovalStatus ApprovalStatus { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateExpenseCommand(
    int FestivalId, int FestivalBudgetCategoryId, int? VendorId, decimal Amount, DateTime ExpenseDate,
    string? Description, ContributionPaymentMethod PaymentMethod, string? BillImageUrl, string? InvoiceNumber)
    : IRequest<int>;

public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.FestivalBudgetCategoryId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.InvoiceNumber).MaximumLength(100);
    }
}

public record UpdateExpenseCommand(
    int Id, int FestivalBudgetCategoryId, int? VendorId, decimal Amount, DateTime ExpenseDate,
    string? Description, ContributionPaymentMethod PaymentMethod, string? BillImageUrl, string? InvoiceNumber)
    : IRequest<Unit>;

public class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FestivalBudgetCategoryId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public record DeleteExpenseCommand(int Id) : IRequest<Unit>;

public record SubmitExpenseCommand(int Id) : IRequest<Unit>;

public record ApproveExpenseCommand(int Id) : IRequest<Unit>;

public record RejectExpenseCommand(int Id, string Reason) : IRequest<Unit>;

public class RejectExpenseCommandValidator : AbstractValidator<RejectExpenseCommand>
{
    public RejectExpenseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public record MarkExpensePaidCommand(int Id) : IRequest<Unit>;

/// <summary>Only Draft/Rejected expenses may be edited or deleted — once
/// Pending/Approved/Paid, the financial trail must stay intact.</summary>
public class FestivalExpenseCommandHandlers :
    IRequestHandler<CreateExpenseCommand, int>,
    IRequestHandler<UpdateExpenseCommand, Unit>,
    IRequestHandler<DeleteExpenseCommand, Unit>,
    IRequestHandler<SubmitExpenseCommand, Unit>,
    IRequestHandler<ApproveExpenseCommand, Unit>,
    IRequestHandler<RejectExpenseCommand, Unit>,
    IRequestHandler<MarkExpensePaidCommand, Unit>
{
    private static readonly ExpenseApprovalStatus[] EditableStatuses = { ExpenseApprovalStatus.Draft, ExpenseApprovalStatus.Rejected };

    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public FestivalExpenseCommandHandlers(
        IApplicationDbContext context, IAuditService auditService,
        ICurrentUserService currentUserService, INotificationService notificationService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<int> Handle(CreateExpenseCommand request, CancellationToken ct)
    {
        if (!await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.FestivalId);
        }

        if (!await _context.FestivalBudgetCategories.AnyAsync(
            c => c.Id == request.FestivalBudgetCategoryId && c.FestivalId == request.FestivalId && !c.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(FestivalBudgetCategory), request.FestivalBudgetCategoryId);
        }

        var expense = new FestivalExpense
        {
            FestivalId = request.FestivalId,
            FestivalBudgetCategoryId = request.FestivalBudgetCategoryId,
            VendorId = request.VendorId,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            Description = request.Description,
            PaymentMethod = request.PaymentMethod,
            BillImageUrl = request.BillImageUrl,
            InvoiceNumber = request.InvoiceNumber,
            ApprovalStatus = ExpenseApprovalStatus.Draft
        };
        await _context.FestivalExpenses.AddAsync(expense, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalExpense), expense.Id.ToString(), ct: ct);
        return expense.Id;
    }

    public async Task<Unit> Handle(UpdateExpenseCommand request, CancellationToken ct)
    {
        var expense = await GetEditableExpenseAsync(request.Id, ct);

        expense.FestivalBudgetCategoryId = request.FestivalBudgetCategoryId;
        expense.VendorId = request.VendorId;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.Description = request.Description;
        expense.PaymentMethod = request.PaymentMethod;
        expense.BillImageUrl = request.BillImageUrl;
        expense.InvoiceNumber = request.InvoiceNumber;
        // Editing a rejected expense sends it back to Draft for a fresh review cycle.
        expense.ApprovalStatus = ExpenseApprovalStatus.Draft;
        expense.RejectionReason = null;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalExpense), expense.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteExpenseCommand request, CancellationToken ct)
    {
        var expense = await GetEditableExpenseAsync(request.Id, ct);
        expense.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalExpense), expense.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(SubmitExpenseCommand request, CancellationToken ct)
    {
        var expense = await GetExpenseAsync(request.Id, ct);
        if (expense.ApprovalStatus != ExpenseApprovalStatus.Draft)
        {
            throw new ConflictAppException("Only draft expenses can be submitted for approval.");
        }

        expense.ApprovalStatus = ExpenseApprovalStatus.Pending;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalExpense), expense.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ApproveExpenseCommand request, CancellationToken ct)
    {
        var expense = await GetExpenseAsync(request.Id, ct);
        if (expense.ApprovalStatus != ExpenseApprovalStatus.Pending)
        {
            throw new ConflictAppException("Only pending expenses can be approved.");
        }

        expense.ApprovalStatus = ExpenseApprovalStatus.Approved;
        expense.ApprovedByUserId = _currentUserService.UserId;
        expense.ApprovedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Approve, "Festivals", nameof(FestivalExpense), expense.Id.ToString(), ct: ct);
        await _notificationService.SendToAllAsync("FestivalExpenseApproved",
            new { festivalId = expense.FestivalId, expenseId = expense.Id, amount = expense.Amount }, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RejectExpenseCommand request, CancellationToken ct)
    {
        var expense = await GetExpenseAsync(request.Id, ct);
        if (expense.ApprovalStatus != ExpenseApprovalStatus.Pending)
        {
            throw new ConflictAppException("Only pending expenses can be rejected.");
        }

        expense.ApprovalStatus = ExpenseApprovalStatus.Rejected;
        expense.RejectionReason = request.Reason;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Reject, "Festivals", nameof(FestivalExpense), expense.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(MarkExpensePaidCommand request, CancellationToken ct)
    {
        var expense = await GetExpenseAsync(request.Id, ct);
        if (expense.ApprovalStatus != ExpenseApprovalStatus.Approved)
        {
            throw new ConflictAppException("Only approved expenses can be marked as paid.");
        }

        expense.ApprovalStatus = ExpenseApprovalStatus.Paid;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Payment, "Festivals", nameof(FestivalExpense), expense.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    private async Task<FestivalExpense> GetExpenseAsync(int id, CancellationToken ct) =>
        await _context.FestivalExpenses.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
        ?? throw new NotFoundException(nameof(FestivalExpense), id);

    private async Task<FestivalExpense> GetEditableExpenseAsync(int id, CancellationToken ct)
    {
        var expense = await GetExpenseAsync(id, ct);
        if (!EditableStatuses.Contains(expense.ApprovalStatus))
        {
            throw new ConflictAppException("Only draft or rejected expenses can be edited or deleted.");
        }
        return expense;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetExpensesQuery(
    int FestivalId, ExpenseApprovalStatus? Status, int? FestivalBudgetCategoryId,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FestivalExpenseDto>>;

public class FestivalExpenseQueryHandlers : IRequestHandler<GetExpensesQuery, PaginatedResult<FestivalExpenseDto>>
{
    private readonly IApplicationDbContext _context;

    public FestivalExpenseQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedResult<FestivalExpenseDto>> Handle(GetExpensesQuery request, CancellationToken ct)
    {
        var query = _context.FestivalExpenses.Where(e => e.FestivalId == request.FestivalId);

        if (request.Status.HasValue) query = query.Where(e => e.ApprovalStatus == request.Status);
        if (request.FestivalBudgetCategoryId.HasValue)
            query = query.Where(e => e.FestivalBudgetCategoryId == request.FestivalBudgetCategoryId);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new FestivalExpenseDto
            {
                Id = e.Id,
                FestivalId = e.FestivalId,
                FestivalBudgetCategoryId = e.FestivalBudgetCategoryId,
                Category = e.FestivalBudgetCategory.Category,
                VendorId = e.VendorId,
                VendorName = e.Vendor != null ? e.Vendor.Name : null,
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                Description = e.Description,
                PaymentMethod = e.PaymentMethod,
                BillImageUrl = e.BillImageUrl,
                InvoiceNumber = e.InvoiceNumber,
                ApprovalStatus = e.ApprovalStatus,
                ApprovedByUserId = e.ApprovedByUserId,
                ApprovedAt = e.ApprovedAt,
                RejectionReason = e.RejectionReason
            })
            .ToListAsync(ct);

        return new PaginatedResult<FestivalExpenseDto>(items, totalCount, pageNumber, pageSize);
    }
}
