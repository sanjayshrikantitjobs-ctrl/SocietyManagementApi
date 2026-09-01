using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One budget line (Decoration, Sound, Food, ...) for a festival.
/// ActualAmount is deliberately not stored here — it is always computed from
/// the sum of Approved/Paid FestivalExpense rows in this category, so the two
/// can never drift out of sync.</summary>
public class FestivalBudgetCategory : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    public FestivalBudgetCategoryType Category { get; set; }

    /// <summary>Set only when Category == Custom — the typed-in name for a
    /// category not in the predefined list. Multiple Custom rows can coexist
    /// per festival (the DB uniqueness constraint on (FestivalId, Category)
    /// is filtered to exclude Custom rows — see the entity configuration).</summary>
    public string? CustomCategoryName { get; set; }

    public decimal EstimatedAmount { get; set; }

    public decimal ApprovedAmount { get; set; }

    public string? Notes { get; set; }

    public ICollection<FestivalBudgetRevision> Revisions { get; set; } = new List<FestivalBudgetRevision>();
    public ICollection<FestivalExpense> Expenses { get; set; } = new List<FestivalExpense>();
}
