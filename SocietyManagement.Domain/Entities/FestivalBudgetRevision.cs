using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>Snapshot written every time a FestivalBudgetCategory's amounts
/// change, giving the "Budget Revision History" the spec requires.</summary>
public class FestivalBudgetRevision : BaseAuditableEntity
{
    public int FestivalBudgetCategoryId { get; set; }
    public FestivalBudgetCategory FestivalBudgetCategory { get; set; } = default!;

    public decimal PreviousEstimatedAmount { get; set; }

    public decimal NewEstimatedAmount { get; set; }

    public decimal PreviousApprovedAmount { get; set; }

    public decimal NewApprovedAmount { get; set; }

    public string? Reason { get; set; }
}
