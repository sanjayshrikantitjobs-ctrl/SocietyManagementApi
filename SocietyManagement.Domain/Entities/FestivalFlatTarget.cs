using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>The annual contribution a flat is expected to pay toward a
/// festival — e.g. "every flat gives ₹5000 for Ganpati." AmountPaid/Outstanding
/// are deliberately not stored here; they're computed from the matching
/// FestivalContribution rows for (FestivalId, FlatId) at query time.</summary>
public class FestivalFlatTarget : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public decimal TargetAmount { get; set; }

    /// <summary>Non-null when this flat has told the society it won't be
    /// contributing — a memo, not a status: the flat's computed
    /// FlatContributionStatus (Pending/PartiallyPaid/Paid) is unaffected, so
    /// they can still pay later without anyone having to "undeclare" this
    /// first.</summary>
    public string? DeclineReason { get; set; }
}
