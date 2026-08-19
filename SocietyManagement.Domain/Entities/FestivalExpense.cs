using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class FestivalExpense : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    public int FestivalBudgetCategoryId { get; set; }
    public FestivalBudgetCategory FestivalBudgetCategory { get; set; } = default!;

    public int? VendorId { get; set; }
    public FestivalVendor? Vendor { get; set; }

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string? Description { get; set; }

    public ContributionPaymentMethod PaymentMethod { get; set; }

    public string? BillImageUrl { get; set; }

    public string? InvoiceNumber { get; set; }

    public ExpenseApprovalStatus ApprovalStatus { get; set; } = ExpenseApprovalStatus.Draft;

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectionReason { get; set; }
}
