using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A general operating expense — electricity, repairs, vendor
/// payments, staff salary payouts, or anything else with nowhere else to
/// be recorded. Festival expenses are NOT stored here — they keep their
/// own FestivalExpense/approval-workflow table and are only read
/// alongside these rows by the Finance module.</summary>
public class Expense : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public ExpenseCategory Category { get; set; }

    public string Title { get; set; } = default!;

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public ContributionPaymentMethod PaymentMethod { get; set; }

    public string? PaidTo { get; set; }

    /// <summary>Set when Category = StaffSalary, to link the payout to the
    /// staff member it was paid to. Optional even then — the free-text
    /// PaidTo field always works as a fallback.</summary>
    public int? StaffId { get; set; }
    public Staff? Staff { get; set; }

    public string? BillImageUrl { get; set; }

    public string? Notes { get; set; }
}
