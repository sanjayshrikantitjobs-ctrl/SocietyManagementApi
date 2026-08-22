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
}
