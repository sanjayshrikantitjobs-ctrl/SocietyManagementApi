using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>Root of the Society Setup hierarchy: Society -> Building -> Wing -> Floor -> Flat.</summary>
public class Society : BaseAuditableEntity
{
    public string Name { get; set; } = default!;

    /// <summary>Short unique code a non-Super-Admin login must supply
    /// alongside identifier+password, so it must match the caller's own
    /// User.SocietyId at login time. Server-generated on create; nullable
    /// because pre-existing societies get one via a startup backfill, not
    /// at insert time.</summary>
    public string? Code { get; set; }

    public string? RegistrationNumber { get; set; }

    public string Address { get; set; } = default!;

    public string City { get; set; } = default!;

    public string State { get; set; } = default!;

    public string Pincode { get; set; } = default!;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? LogoUrl { get; set; }

    public DateTime? EstablishedDate { get; set; }

    /// <summary>Every society carries an explicit subscription window — a
    /// free trial or a paid validity period, set by the Super Admin (see
    /// SetSocietySubscriptionCommand). SubscriptionActiveFilter checks
    /// SubscriptionEndDate on every request from a non-Super-Admin user of
    /// this society; expired means blocked until extended. Not nullable —
    /// existing societies are backfilled with a 1-year window by the
    /// migration that introduces these columns, rather than being exempt.</summary>
    public DateTime SubscriptionStartDate { get; set; }

    public DateTime SubscriptionEndDate { get; set; }

    /// <summary>Manual Super Admin override, independent of the date window
    /// above — lets a society be cut off immediately (e.g. non-payment) or
    /// reinstated without doing date math, matching "restrict... until super
    /// admin enable it" from the original request. Checked by
    /// SubscriptionActiveFilter alongside SubscriptionEndDate; either one
    /// blocks. Defaults false (not suspended) for every existing and new
    /// society.</summary>
    public bool IsSubscriptionSuspended { get; set; }

    public ICollection<Building> Buildings { get; set; } = new List<Building>();

    public ICollection<ParkingSlot> ParkingSlots { get; set; } = new List<ParkingSlot>();
}
