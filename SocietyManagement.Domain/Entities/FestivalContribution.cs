using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A member (or anonymous donor's) contribution towards a festival.</summary>
public class FestivalContribution : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    /// <summary>Null for anonymous donations.</summary>
    public int? FlatId { get; set; }
    public Flat? Flat { get; set; }

    /// <summary>Denormalized so receipts/lists don't need a join for anonymous
    /// or guest donors who have no FlatId.</summary>
    public string MemberName { get; set; } = default!;

    public decimal Amount { get; set; }

    public ContributionPaymentMethod PaymentMethod { get; set; }

    public DateTime PaymentDate { get; set; }

    public string? TransactionId { get; set; }

    public string ReceiptNumber { get; set; } = default!;

    public bool IsAnonymous { get; set; }

    /// <summary>The number the receipt was actually WhatsApp'd to — an
    /// explicit override at record time, or the flat's resolved phone
    /// (primary FlatResidency contact, else Flat.OwnerPhone). Null if
    /// neither was available (e.g. a Guest contribution with no number
    /// given) — no WhatsApp send happens in that case.</summary>
    public string? WhatsAppNumber { get; set; }
}
