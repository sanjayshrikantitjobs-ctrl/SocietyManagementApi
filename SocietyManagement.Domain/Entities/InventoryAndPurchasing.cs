using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A consumable/stock item the society keeps (bulbs, cleaning
/// supplies...). Current stock is never stored — it is always the sum of
/// StockTransaction.Quantity, so it can't drift from its history.</summary>
public class InventoryItem : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Category { get; set; }
    public string Unit { get; set; } = "Units";
    public decimal MinimumStock { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One signed movement of stock: In is positive, Out is negative,
/// Adjustment is either sign (stock-take corrections).</summary>
public class StockTransaction : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = default!;

    public StockTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? IssuedTo { get; set; }
    public string? LocationOfUse { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when the movement came from receiving a purchase.</summary>
    public int? PurchaseRequestId { get; set; }
}

/// <summary>Purchase request that carries straight through approval, order
/// and receipt on one record — a deliberately light procurement flow rather
/// than separate PR/quotation/PO entities.</summary>
public class PurchaseRequest : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public PurchasePriority Priority { get; set; } = PurchasePriority.Normal;
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;
    public DateTime? DueDate { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = default!;

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? OrderedAt { get; set; }

    public ICollection<PurchaseRequestItem> Items { get; set; } = new List<PurchaseRequestItem>();
}

public class PurchaseRequestItem : BaseEntity
{
    public int PurchaseRequestId { get; set; }
    public PurchaseRequest PurchaseRequest { get; set; } = default!;

    public string ItemName { get; set; } = default!;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "Units";
    public decimal EstimatedUnitPrice { get; set; }
    public decimal ReceivedQuantity { get; set; }

    /// <summary>When set, receiving this line adds stock to that item.</summary>
    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}

/// <summary>Yearly budget for one expense category (financial year =
/// April to March, identified by its starting calendar year).</summary>
public class Budget : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int FinancialYear { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
}
