using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Finance;

public class ExpenseDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Title { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public ContributionPaymentMethod PaymentMethod { get; set; }
    public string? PaidTo { get; set; }
    public int? StaffId { get; set; }
    public string? StaffName { get; set; }
    public string? BillImageUrl { get; set; }
    public string? Notes { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateGeneralExpenseCommand(
    int SocietyId, ExpenseCategory Category, string Title, decimal Amount, DateTime ExpenseDate,
    ContributionPaymentMethod PaymentMethod, string? PaidTo, int? StaffId, string? BillImageUrl, string? Notes)
    : IRequest<int>;

public class CreateGeneralExpenseCommandValidator : AbstractValidator<CreateGeneralExpenseCommand>
{
    public CreateGeneralExpenseCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaidTo).MaximumLength(150);
    }
}

public record UpdateGeneralExpenseCommand(
    int Id, ExpenseCategory Category, string Title, decimal Amount, DateTime ExpenseDate,
    ContributionPaymentMethod PaymentMethod, string? PaidTo, int? StaffId, string? BillImageUrl, string? Notes)
    : IRequest<Unit>;

public class UpdateGeneralExpenseCommandValidator : AbstractValidator<UpdateGeneralExpenseCommand>
{
    public UpdateGeneralExpenseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaidTo).MaximumLength(150);
    }
}

public record DeleteGeneralExpenseCommand(int Id) : IRequest<Unit>;

public class FinanceExpenseCommandHandlers :
    IRequestHandler<CreateGeneralExpenseCommand, int>,
    IRequestHandler<UpdateGeneralExpenseCommand, Unit>,
    IRequestHandler<DeleteGeneralExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FinanceExpenseCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateGeneralExpenseCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        if (request.StaffId.HasValue && !await _context.Staff.AnyAsync(s => s.Id == request.StaffId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Domain.Entities.Staff), request.StaffId.Value);
        }

        var expense = new Expense
        {
            SocietyId = request.SocietyId, Category = request.Category, Title = request.Title, Amount = request.Amount,
            ExpenseDate = request.ExpenseDate, PaymentMethod = request.PaymentMethod, PaidTo = request.PaidTo,
            StaffId = request.StaffId, BillImageUrl = request.BillImageUrl, Notes = request.Notes
        };
        await _context.Expenses.AddAsync(expense, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Finance", nameof(Expense), expense.Id.ToString(), ct: ct);
        return expense.Id;
    }

    public async Task<Unit> Handle(UpdateGeneralExpenseCommand request, CancellationToken ct)
    {
        var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Expense), request.Id);

        if (request.StaffId.HasValue && !await _context.Staff.AnyAsync(s => s.Id == request.StaffId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Domain.Entities.Staff), request.StaffId.Value);
        }

        expense.Category = request.Category;
        expense.Title = request.Title;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.PaymentMethod = request.PaymentMethod;
        expense.PaidTo = request.PaidTo;
        expense.StaffId = request.StaffId;
        expense.BillImageUrl = request.BillImageUrl;
        expense.Notes = request.Notes;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Finance", nameof(Expense), expense.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteGeneralExpenseCommand request, CancellationToken ct)
    {
        var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Expense), request.Id);
        expense.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Finance", nameof(Expense), expense.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetExpenseByIdQuery(int Id) : IRequest<ExpenseDto>;

/// <summary>Unified list — general Expense rows plus Approved/Paid
/// FestivalExpense rows (read-only here; still edited from the Festivals
/// module). Category filtering only applies to general rows, since
/// ExpenseCategory has no Festival-expense equivalent — setting it drops
/// Festival rows from the result entirely.</summary>
public record GetFinanceExpensesQuery(
    int SocietyId, FinanceSource? Source, ExpenseCategory? Category, DateTime? DateFrom, DateTime? DateTo, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FinanceExpenseRowDto>>;

public class FinanceExpenseQueryHandlers :
    IRequestHandler<GetExpenseByIdQuery, ExpenseDto>,
    IRequestHandler<GetFinanceExpensesQuery, PaginatedResult<FinanceExpenseRowDto>>
{
    private readonly IApplicationDbContext _context;

    public FinanceExpenseQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<ExpenseDto> Handle(GetExpenseByIdQuery request, CancellationToken ct)
    {
        var expense = await _context.Expenses.Where(e => e.Id == request.Id && !e.IsDeleted)
            .Select(e => new ExpenseDto
            {
                Id = e.Id, SocietyId = e.SocietyId, Category = e.Category, Title = e.Title, Amount = e.Amount,
                ExpenseDate = e.ExpenseDate, PaymentMethod = e.PaymentMethod, PaidTo = e.PaidTo, StaffId = e.StaffId,
                StaffName = e.Staff != null ? e.Staff.FirstName + " " + e.Staff.LastName : null,
                BillImageUrl = e.BillImageUrl, Notes = e.Notes
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Expense), request.Id);
        return expense;
    }

    public async Task<PaginatedResult<FinanceExpenseRowDto>> Handle(GetFinanceExpensesQuery request, CancellationToken ct)
    {
        var rows = await FinanceQueryHelpers.GetExpenseRowsAsync(_context, request.SocietyId, request.DateFrom, request.DateTo, ct);

        IEnumerable<FinanceExpenseRowDto> filtered = rows;
        if (request.Category.HasValue)
        {
            filtered = filtered.Where(r => r.Source == FinanceSource.GeneralExpense && r.CategoryLabel == FinanceQueryHelpers.ExpenseCategoryLabel(request.Category.Value));
        }
        if (request.Source.HasValue) filtered = filtered.Where(r => r.Source == request.Source);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(r => r.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (r.PaidTo != null && r.PaidTo.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var all = filtered.OrderByDescending(r => r.ExpenseDate).ToList();
        var totalCount = all.Count;
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var items = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedResult<FinanceExpenseRowDto>(items, totalCount, pageNumber, pageSize);
    }
}
