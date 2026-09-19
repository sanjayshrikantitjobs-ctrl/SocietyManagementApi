using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Inventory;

public class InventoryItemDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public string? Category { get; set; }
    public string Unit { get; set; } = default!;
    public decimal MinimumStock { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentStock { get; set; }
    public bool IsLow { get; set; }
}

public class StockTransactionDto
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string ItemName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public StockTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? IssuedTo { get; set; }
    public string? LocationOfUse { get; set; }
    public string? Notes { get; set; }
    public int? PurchaseRequestId { get; set; }
}

public record CreateInventoryItemCommand(
    int SocietyId, string Name, string? Category, string Unit, decimal MinimumStock, decimal OpeningStock) : IRequest<int>;

public class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OpeningStock).GreaterThanOrEqualTo(0);
    }
}

public record UpdateInventoryItemCommand(int Id, string Name, string? Category, string Unit, decimal MinimumStock, bool IsActive) : IRequest<Unit>;

public class UpdateInventoryItemCommandValidator : AbstractValidator<UpdateInventoryItemCommand>
{
    public UpdateInventoryItemCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
    }
}

public record DeleteInventoryItemCommand(int Id) : IRequest<Unit>;

/// <summary>Quantity is always entered as a positive amount for In/Out (the
/// handler applies the sign); for Adjustment it is the signed correction.</summary>
public record RecordStockTransactionCommand(
    int InventoryItemId, StockTransactionType Type, decimal Quantity, DateTime TransactionDate,
    string? IssuedTo, string? LocationOfUse, string? Notes) : IRequest<int>;

public class RecordStockTransactionCommandValidator : AbstractValidator<RecordStockTransactionCommand>
{
    public RecordStockTransactionCommandValidator()
    {
        RuleFor(x => x.InventoryItemId).GreaterThan(0);
        RuleFor(x => x.Quantity).NotEqual(0);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Type != StockTransactionType.Adjustment)
            .WithMessage("Quantity must be greater than zero.");
        RuleFor(x => x.IssuedTo).MaximumLength(150);
        RuleFor(x => x.LocationOfUse).MaximumLength(150);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public record GetInventoryItemsQuery(
    int SocietyId, string? Search, bool LowStockOnly, bool ActiveOnly,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<InventoryItemDto>>;

public record GetStockTransactionsQuery(
    int SocietyId, int? InventoryItemId, StockTransactionType? Type, DateTime? From, DateTime? To,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<StockTransactionDto>>;

public class InventoryHandlers :
    IRequestHandler<CreateInventoryItemCommand, int>,
    IRequestHandler<UpdateInventoryItemCommand, Unit>,
    IRequestHandler<DeleteInventoryItemCommand, Unit>,
    IRequestHandler<RecordStockTransactionCommand, int>,
    IRequestHandler<GetInventoryItemsQuery, PaginatedResult<InventoryItemDto>>,
    IRequestHandler<GetStockTransactionsQuery, PaginatedResult<StockTransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public InventoryHandlers(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    private async Task<InventoryItem> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException(nameof(InventoryItem), id);
        if (_currentUser.SocietyId.HasValue && _currentUser.SocietyId != item.SocietyId) throw new NotFoundException(nameof(InventoryItem), id);
        return item;
    }

    public async Task<int> Handle(CreateInventoryItemCommand r, CancellationToken ct)
    {
        var item = new InventoryItem
        {
            SocietyId = r.SocietyId, Name = r.Name.Trim(), Category = r.Category, Unit = r.Unit.Trim(), MinimumStock = r.MinimumStock, IsActive = true
        };
        await _context.InventoryItems.AddAsync(item, ct);
        await _context.SaveChangesAsync(ct);

        // Opening stock is just the first In transaction, so history stays complete.
        if (r.OpeningStock > 0)
        {
            await _context.StockTransactions.AddAsync(new StockTransaction
            {
                SocietyId = item.SocietyId, InventoryItemId = item.Id, Type = StockTransactionType.In, Quantity = r.OpeningStock,
                TransactionDate = DateTime.UtcNow.Date, Notes = "Opening stock"
            }, ct);
            await _context.SaveChangesAsync(ct);
        }
        await _auditService.LogAsync(AuditAction.Create, "Inventory", nameof(InventoryItem), item.Id.ToString(), newValues: new { item.Name }, ct: ct);
        return item.Id;
    }

    public async Task<Unit> Handle(UpdateInventoryItemCommand r, CancellationToken ct)
    {
        var item = await LoadOwnedAsync(r.Id, ct);
        item.Name = r.Name.Trim(); item.Category = r.Category; item.Unit = r.Unit.Trim(); item.MinimumStock = r.MinimumStock; item.IsActive = r.IsActive;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Inventory", nameof(InventoryItem), item.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteInventoryItemCommand r, CancellationToken ct)
    {
        var item = await LoadOwnedAsync(r.Id, ct);
        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Inventory", nameof(InventoryItem), item.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<int> Handle(RecordStockTransactionCommand r, CancellationToken ct)
    {
        var item = await LoadOwnedAsync(r.InventoryItemId, ct);
        var current = await _context.StockTransactions.Where(t => t.InventoryItemId == item.Id).SumAsync(t => (decimal?)t.Quantity, ct) ?? 0m;

        var signed = r.Type switch
        {
            StockTransactionType.In => r.Quantity,
            StockTransactionType.Out => -r.Quantity,
            _ => r.Quantity
        };
        if (current + signed < 0)
            throw new ConflictAppException($"Only {current:0.##} {item.Unit} of {item.Name} in stock.");

        var tx = new StockTransaction
        {
            SocietyId = item.SocietyId, InventoryItemId = item.Id, Type = r.Type, Quantity = signed, TransactionDate = r.TransactionDate.Date,
            IssuedTo = r.IssuedTo, LocationOfUse = r.LocationOfUse, Notes = r.Notes
        };
        await _context.StockTransactions.AddAsync(tx, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Inventory", nameof(StockTransaction), tx.Id.ToString(),
            newValues: new { item.Name, r.Type, signed }, ct: ct);
        return tx.Id;
    }

    public async Task<PaginatedResult<InventoryItemDto>> Handle(GetInventoryItemsQuery r, CancellationToken ct)
    {
        var query = _context.InventoryItems.Where(i => i.SocietyId == r.SocietyId);
        if (r.ActiveOnly) query = query.Where(i => i.IsActive);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term) || (i.Category != null && i.Category.ToLower().Contains(term)));
        }

        var projected = query.Select(i => new InventoryItemDto
        {
            Id = i.Id, SocietyId = i.SocietyId, Name = i.Name, Category = i.Category, Unit = i.Unit, MinimumStock = i.MinimumStock, IsActive = i.IsActive,
            CurrentStock = _context.StockTransactions.Where(t => t.InventoryItemId == i.Id).Sum(t => (decimal?)t.Quantity) ?? 0m
        });
        if (r.LowStockOnly) projected = projected.Where(i => i.CurrentStock <= i.MinimumStock && i.MinimumStock > 0);

        var total = await projected.CountAsync(ct);
        var pageSize = Math.Clamp(r.PageSize, 1, AppConstants.MaxPageSize);
        var page = Math.Max(r.PageNumber, 1);
        var items = await projected.OrderBy(i => i.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        foreach (var i in items) i.IsLow = i.MinimumStock > 0 && i.CurrentStock <= i.MinimumStock;
        return new PaginatedResult<InventoryItemDto>(items, total, page, pageSize);
    }

    public async Task<PaginatedResult<StockTransactionDto>> Handle(GetStockTransactionsQuery r, CancellationToken ct)
    {
        var query = _context.StockTransactions.Where(t => t.SocietyId == r.SocietyId);
        if (r.InventoryItemId.HasValue) query = query.Where(t => t.InventoryItemId == r.InventoryItemId);
        if (r.Type.HasValue) query = query.Where(t => t.Type == r.Type);
        if (r.From.HasValue) query = query.Where(t => t.TransactionDate >= r.From.Value.Date);
        if (r.To.HasValue) query = query.Where(t => t.TransactionDate <= r.To.Value.Date);

        var total = await query.CountAsync(ct);
        var pageSize = Math.Clamp(r.PageSize, 1, AppConstants.MaxPageSize);
        var page = Math.Max(r.PageNumber, 1);
        var items = await query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new StockTransactionDto
            {
                Id = t.Id, InventoryItemId = t.InventoryItemId, ItemName = t.InventoryItem.Name, Unit = t.InventoryItem.Unit, Type = t.Type,
                Quantity = t.Quantity, TransactionDate = t.TransactionDate, IssuedTo = t.IssuedTo, LocationOfUse = t.LocationOfUse,
                Notes = t.Notes, PurchaseRequestId = t.PurchaseRequestId
            }).ToListAsync(ct);
        return new PaginatedResult<StockTransactionDto>(items, total, page, pageSize);
    }
}
