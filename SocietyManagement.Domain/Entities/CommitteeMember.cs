using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>A standalone society-level directory entry (Chairman/Secretary/
/// Treasurer/etc.) — deliberately independent of the two parallel resident
/// models (Person/OccupancyMember and Member/FlatResidency), since a
/// committee role isn't tied to which of those a given resident happens to
/// be recorded under, and keeping it standalone avoids syncing the same
/// field across both.</summary>
public class CommitteeMember : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Free text ("Chairman"/"Secretary"/"Treasurer"/...) rather
    /// than an enum, so a society can add a role like "Joint Secretary"
    /// without a code change.</summary>
    public string Designation { get; set; } = default!;

    public string? FlatNumber { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public int DisplayOrder { get; set; }
}
