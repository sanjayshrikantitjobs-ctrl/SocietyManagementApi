using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Purchasing;

public class PurchaseItemDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = default!;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = default!;
    public decimal EstimatedUnitPrice { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public int? InventoryItemId { get; set; }
}

public class PurchaseRequestDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public PurchasePriority Priority { get; set; }
    public PurchaseStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string RequestedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? OrderedAt { get; set; }
    public decimal EstimatedTotal { get; set; }
    public List<PurchaseItemDto> Items { get; set; } = new();
}

public record PurchaseItemInput(string ItemName, decimal Quantity, string Unit, decimal EstimatedUnitPrice, int? InventoryItemId);

public record CreatePurchaseRequestCommand(
    int SocietyId, string Title, string? Description, PurchasePriority Priority, DateTime? DueDate, int? VendorId,
    List<PurchaseItemInput> Items, bool Submit) : IRequest<int>;

public class PurchaseItemInputValidator : AbstractValidator<PurchaseItemInput>
{
    public PurchaseItemInputValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.EstimatedUnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class CreatePurchaseRequestCommandValidator : AbstractValidator<CreatePurchaseRequestCommand>
{
    public CreatePurchaseRequestCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Add at least one item.");
        RuleForEach(x => x.Items).SetValidator(new PurchaseItemInputValidator());
    }
}

// Editing is only allowed while the request is still Draft or was Rejected.
public record UpdatePurchaseRequestCommand(
    int Id, string Title, string? Description, PurchasePriority Priority, DateTime? DueDate, int? VendorId,
    List<PurchaseItemInput> Items) : IRequest<Unit>;

public class UpdatePurchaseRequestCommandValidator : AbstractValidator<UpdatePurchaseRequestCommand>
{
    public UpdatePurchaseRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Add at least one item.");
        RuleForEach(x => x.Items).SetValidator(new PurchaseItemInputValidator());
    }
}

public record SubmitPurchaseRequestCommand(int Id) : IRequest<Unit>;
public record ApprovePurchaseRequestCommand(int Id) : IRequest<Unit>;
public record RejectPurchaseRequestCommand(int Id, string Reason) : IRequest<Unit>;
public record MarkPurchaseOrderedCommand(int Id, int? VendorId) : IRequest<Unit>;
public record CancelPurchaseRequestCommand(int Id) : IRequest<Unit>;

public class RejectPurchaseRequestCommandValidator : AbstractValidator<RejectPurchaseRequestCommand>
{
    public RejectPurchaseRequestCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public record ReceiveLine(int ItemId, decimal Quantity);

public record ReceivePurchaseCommand(int Id, List<ReceiveLine> Lines) : IRequest<Unit>;

public class ReceivePurchaseCommandValidator : AbstractValidator<ReceivePurchaseCommand>
{
    public ReceivePurchaseCommandValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0));
    }
}

public record GetPurchaseRequestsQuery(
    int SocietyId, string? Search, PurchaseStatus? Status,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<PurchaseRequestDto>>;

public record GetPurchaseRequestByIdQuery(int Id) : IRequest<PurchaseRequestDto>;

public class PurchaseHandlers :
    IRequestHandler<CreatePurchaseRequestCommand, int>,
    IRequestHandler<UpdatePurchaseRequestCommand, Unit>,
    IRequestHandler<SubmitPurchaseRequestCommand, Unit>,
    IRequestHandler<ApprovePurchaseRequestCommand, Unit>,
    IRequestHandler<RejectPurchaseRequestCommand, Unit>,
    IRequestHandler<MarkPurchaseOrderedCommand, Unit>,
    IRequestHandler<CancelPurchaseRequestCommand, Unit>,
    IRequestHandler<ReceivePurchaseCommand, Unit>,
    IRequestHandler<GetPurchaseRequestsQuery, PaginatedResult<PurchaseRequestDto>>,
    IRequestHandler<GetPurchaseRequestByIdQuery, PurchaseRequestDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public PurchaseHandlers(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    private async Task<PurchaseRequest> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var p = await _context.PurchaseRequests.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(PurchaseRequest), id);
        if (_currentUser.SocietyId.HasValue && _currentUser.SocietyId != p.SocietyId) throw new NotFoundException(nameof(PurchaseRequest), id);
        return p;
    }

    private static void RequireStatus(PurchaseRequest p, string action, params PurchaseStatus[] allowed)
    {
        if (!allowed.Contains(p.Status))
            throw new ConflictAppException($"A request that is {p.Status} can't be {action}.");
    }

    private async Task EnsureLinksInSocietyAsync(int societyId, int? vendorId, IEnumerable<PurchaseItemInput> items, CancellationToken ct)
    {
        if (vendorId.HasValue && !await _context.Vendors.AnyAsync(v => v.Id == vendorId && v.SocietyId == societyId, ct))
            throw new NotFoundException(nameof(Vendor), vendorId.Value);
        var itemIds = items.Where(i => i.InventoryItemId.HasValue).Select(i => i.InventoryItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            var found = await _context.InventoryItems.CountAsync(i => itemIds.Contains(i.Id) && i.SocietyId == societyId, ct);
            if (found != itemIds.Count) throw new NotFoundException(nameof(InventoryItem), itemIds[0]);
        }
    }

    private static List<PurchaseRequestItem> ToItems(IEnumerable<PurchaseItemInput> inputs) =>
        inputs.Select(i => new PurchaseRequestItem
        {
            ItemName = i.ItemName.Trim(), Quantity = i.Quantity, Unit = i.Unit.Trim(), EstimatedUnitPrice = i.EstimatedUnitPrice,
            InventoryItemId = i.InventoryItemId
        }).ToList();

    public async Task<int> Handle(CreatePurchaseRequestCommand r, CancellationToken ct)
    {
        await EnsureLinksInSocietyAsync(r.SocietyId, r.VendorId, r.Items, ct);
        var userId = _currentUser.UserId ?? throw new BadRequestAppException("Sign in required.");
        var name = await _context.Users.Where(u => u.Id == userId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct) ?? "User";

        var p = new PurchaseRequest
        {
            SocietyId = r.SocietyId, Title = r.Title.Trim(), Description = r.Description, Priority = r.Priority, DueDate = r.DueDate,
            VendorId = r.VendorId, RequestedByUserId = userId, RequestedByName = name.Trim(),
            Status = r.Submit ? PurchaseStatus.PendingApproval : PurchaseStatus.Draft, Items = ToItems(r.Items)
        };
        await _context.PurchaseRequests.AddAsync(p, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Purchases", nameof(PurchaseRequest), p.Id.ToString(), newValues: new { p.Title, p.Status }, ct: ct);
        return p.Id;
    }

    public async Task<Unit> Handle(UpdatePurchaseRequestCommand r, CancellationToken ct)
    {
        var p = await LoadOwnedAsync(r.Id, ct);
        RequireStatus(p, "edited", PurchaseStatus.Draft, PurchaseStatus.Rejected);
        await EnsureLinksInSocietyAsync(p.SocietyId, r.VendorId, r.Items, ct);

        p.Title = r.Title.Trim(); p.Description = r.Description; p.Priority = r.Priority; p.DueDate = r.DueDate; p.VendorId = r.VendorId;
        _context.PurchaseRequestItems.RemoveRange(p.Items);
        p.Items = ToItems(r.Items);
        // A rejected request goes back to draft once it is edited.
        p.Status = PurchaseStatus.Draft;
        p.RejectionReason = null;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Purchases", nameof(PurchaseRequest), p.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    private async Task TransitionAsync(int id, string action, PurchaseStatus[] from, Action<PurchaseRequest> apply, CancellationToken ct)
    {
        var p = await LoadOwnedAsync(id, ct);
        RequireStatus(p, action, from);
        apply(p);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Purchases", nameof(PurchaseRequest), p.Id.ToString(), newValues: new { p.Status }, ct: ct);
    }

    public async Task<Unit> Handle(SubmitPurchaseRequestCommand r, CancellationToken ct)
    {
        await TransitionAsync(r.Id, "submitted", new[] { PurchaseStatus.Draft }, p => p.Status = PurchaseStatus.PendingApproval, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ApprovePurchaseRequestCommand r, CancellationToken ct)
    {
        await TransitionAsync(r.Id, "approved", new[] { PurchaseStatus.PendingApproval }, p =>
        {
            p.Status = PurchaseStatus.Approved; p.ApprovedByUserId = _currentUser.UserId; p.ApprovedAt = DateTime.UtcNow; p.RejectionReason = null;
        }, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RejectPurchaseRequestCommand r, CancellationToken ct)
    {
        await TransitionAsync(r.Id, "rejected", new[] { PurchaseStatus.PendingApproval }, p =>
        {
            p.Status = PurchaseStatus.Rejected; p.RejectionReason = r.Reason;
        }, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(MarkPurchaseOrderedCommand r, CancellationToken ct)
    {
        var p = await LoadOwnedAsync(r.Id, ct);
        RequireStatus(p, "ordered", PurchaseStatus.Approved);
        var vendorId = r.VendorId ?? p.VendorId;
        if (vendorId is null) throw new BadRequestAppException("Choose a vendor before placing the order.");
        if (!await _context.Vendors.AnyAsync(v => v.Id == vendorId && v.SocietyId == p.SocietyId, ct)) throw new NotFoundException(nameof(Vendor), vendorId.Value);

        p.VendorId = vendorId;
        p.Status = PurchaseStatus.Ordered;
        p.OrderedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Purchases", nameof(PurchaseRequest), p.Id.ToString(), newValues: new { p.Status, vendorId }, ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(CancelPurchaseRequestCommand r, CancellationToken ct)
    {
        var p = await LoadOwnedAsync(r.Id, ct);
        RequireStatus(p, "cancelled", PurchaseStatus.Draft, PurchaseStatus.PendingApproval, PurchaseStatus.Approved, PurchaseStatus.Ordered);
        if (p.Items.Any(i => i.ReceivedQuantity > 0)) throw new ConflictAppException("Goods have already been received against this order.");
        p.Status = PurchaseStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Purchases", nameof(PurchaseRequest), p.Id.ToString(), newValues: new { p.Status }, ct: ct);
        return Unit.Value;
    }

    // Receiving adds stock (as normal In transactions) for lines linked to an
    // inventory item, and moves the order to PartiallyReceived/Received.
    public async Task<Unit> Handle(ReceivePurchaseCommand r, CancellationToken ct)
    {
        var p = await LoadOwnedAsync(r.Id, ct);
        RequireStatus(p, "received", PurchaseStatus.Ordered, PurchaseStatus.PartiallyReceived);

        foreach (var line in r.Lines)
        {
            var item = p.Items.FirstOrDefault(i => i.Id == line.ItemId) ?? throw new NotFoundException(nameof(PurchaseRequestItem), line.ItemId);
            if (item.ReceivedQuantity + line.Quantity > item.Quantity)
                throw new ConflictAppException($"{item.ItemName}: only {item.Quantity - item.ReceivedQuantity:0.##} {item.Unit} still to receive.");
            item.ReceivedQuantity += line.Quantity;

            if (item.InventoryItemId.HasValue)
            {
                await _context.StockTransactions.AddAsync(new StockTransaction
                {
                    SocietyId = p.SocietyId, InventoryItemId = item.InventoryItemId.Value, Type = StockTransactionType.In, Quantity = line.Quantity,
                    TransactionDate = DateTime.UtcNow.Date, Notes = $"Received against purchase #{p.Id}", PurchaseRequestId = p.Id
                }, ct);
            }
        }

        p.Status = p.Items.All(i => i.ReceivedQuantity >= i.Quantity) ? PurchaseStatus.Received : PurchaseStatus.PartiallyReceived;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Purchases", nameof(PurchaseRequest), p.Id.ToString(), newValues: new { p.Status }, ct: ct);
        return Unit.Value;
    }

    private static PurchaseRequestDto Map(PurchaseRequest p) => new()
    {
        Id = p.Id, SocietyId = p.SocietyId, Title = p.Title, Description = p.Description, Priority = p.Priority, Status = p.Status,
        DueDate = p.DueDate, VendorId = p.VendorId, VendorName = p.Vendor?.Name, RequestedByName = p.RequestedByName, CreatedAt = p.CreatedAt,
        ApprovedAt = p.ApprovedAt, RejectionReason = p.RejectionReason, OrderedAt = p.OrderedAt,
        EstimatedTotal = p.Items.Sum(i => i.Quantity * i.EstimatedUnitPrice),
        Items = p.Items.Select(i => new PurchaseItemDto
        {
            Id = i.Id, ItemName = i.ItemName, Quantity = i.Quantity, Unit = i.Unit, EstimatedUnitPrice = i.EstimatedUnitPrice,
            ReceivedQuantity = i.ReceivedQuantity, InventoryItemId = i.InventoryItemId
        }).ToList()
    };

    public async Task<PaginatedResult<PurchaseRequestDto>> Handle(GetPurchaseRequestsQuery r, CancellationToken ct)
    {
        var query = _context.PurchaseRequests.Where(p => p.SocietyId == r.SocietyId);
        if (r.Status.HasValue) query = query.Where(p => p.Status == r.Status);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(term) || p.RequestedByName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var pageSize = Math.Clamp(r.PageSize, 1, AppConstants.MaxPageSize);
        var page = Math.Max(r.PageNumber, 1);
        var rows = await query.Include(p => p.Vendor).Include(p => p.Items)
            .OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PaginatedResult<PurchaseRequestDto>(rows.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<PurchaseRequestDto> Handle(GetPurchaseRequestByIdQuery r, CancellationToken ct)
    {
        var p = await _context.PurchaseRequests.Include(x => x.Vendor).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new NotFoundException(nameof(PurchaseRequest), r.Id);
        if (_currentUser.SocietyId.HasValue && _currentUser.SocietyId != p.SocietyId) throw new NotFoundException(nameof(PurchaseRequest), r.Id);
        return Map(p);
    }
}
