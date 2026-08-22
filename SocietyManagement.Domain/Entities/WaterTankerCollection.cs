using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One flat's water tanker contribution for one calendar month —
/// same "everyone owes a flat rate, some have paid" shape as
/// FestivalFlatTarget, but self-contained (paid/pending lives on this row
/// directly) since there's no separate free-form payment ledger to total
/// against, unlike festival contributions.</summary>
public class WaterTankerCollection : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    /// <summary>Always the first of the month — the billing period, not a payment date.</summary>
    public DateTime Month { get; set; }

    public decimal Amount { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? Notes { get; set; }
}
