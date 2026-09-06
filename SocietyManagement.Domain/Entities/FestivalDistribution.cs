using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A generic festival giveaway/distribution campaign — Kurta,
/// T-shirt, gift, prasad, coupon, anything a festival hands out to
/// eligible flats. Eligibility reuses the existing FestivalContribution/
/// FestivalFlatTarget data rather than inventing a separate mechanism —
/// see DistributionEligibilityType for the exact rule options. Flats can
/// also be added one at a time regardless of eligibility (a manual
/// override — see AddManualClaimCommand), for the "give it to this flat
/// anyway" case.</summary>
public class FestivalDistribution : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    public string ItemName { get; set; } = default!;

    public string? Description { get; set; }

    public DistributionEligibilityType EligibilityType { get; set; } = DistributionEligibilityType.AllFlats;

    /// <summary>Only meaningful when EligibilityType == MinimumAmount.</summary>
    public decimal? EligibilityMinContribution { get; set; }

    /// <summary>Default number of units an eligible flat receives — the
    /// building block for member-based allocation, since generating claims
    /// creates exactly this many per-flat slots, each independently
    /// assignable to one household member.</summary>
    public int QuantityPerFlat { get; set; } = 1;

    public FestivalDistributionStatus Status { get; set; } = FestivalDistributionStatus.Draft;

    public ICollection<FestivalDistributionVariant> Variants { get; set; } = new List<FestivalDistributionVariant>();
    public ICollection<FestivalDistributionClaim> Claims { get; set; } = new List<FestivalDistributionClaim>();
}

/// <summary>One selectable option for the distributed item — a Kurta size
/// (L, XL, XXL...), a color, a gift choice. A distribution with no
/// variants (e.g. Prasad) simply has an empty list, and claims skip
/// variant selection entirely.</summary>
public class FestivalDistributionVariant : BaseAuditableEntity
{
    public int FestivalDistributionId { get; set; }
    public FestivalDistribution FestivalDistribution { get; set; } = default!;

    public string Label { get; set; } = default!;

    public int SortOrder { get; set; }
}

/// <summary>One unit/slot of the distribution for one flat — the atomic
/// tracking unit the whole feature is built around. Generated in bulk
/// (QuantityPerFlat slots per eligible flat) with MemberId/VariantId both
/// null; a resident of that flat later claims a slot for themselves by
/// picking a variant, which also sets MemberId and moves Pending ->
/// Confirmed. Admin marks Confirmed -> Distributed once handed over.</summary>
public class FestivalDistributionClaim : BaseAuditableEntity
{
    public int FestivalDistributionId { get; set; }
    public FestivalDistribution FestivalDistribution { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    /// <summary>1-based position among this flat's slots for this
    /// distribution — purely for stable display ordering, not identity.</summary>
    public int SlotNumber { get; set; }

    /// <summary>Which household member this slot is for — set when a
    /// resident claims the slot, optionally reassignable by an admin.
    /// Points at Person (the Occupancy household-member model), not the
    /// older Member/FlatResidency model — a flat's actual family roster
    /// (spouse, kids, tenant's family, etc.) lives there, not in Member,
    /// which only ever has one row per Owner/Tenant login. Named MemberId
    /// for API/frontend stability, not because it references Member.</summary>
    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    public int? VariantId { get; set; }
    public FestivalDistributionVariant? Variant { get; set; }

    public DistributionClaimStatus Status { get; set; } = DistributionClaimStatus.Pending;

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? DistributedAt { get; set; }
    public int? DistributedByUserId { get; set; }

    /// <summary>Set (non-null) for a paid add-on slot beyond the
    /// distribution's free QuantityPerFlat allocation — the amount charged
    /// to the flat for this one unit via a linked SpecialCharge. Null means
    /// this slot is part of the standard free allocation.</summary>
    public decimal? Amount { get; set; }

    public string? Notes { get; set; }
}
