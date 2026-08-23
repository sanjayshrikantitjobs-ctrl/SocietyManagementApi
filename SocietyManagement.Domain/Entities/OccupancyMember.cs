using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One person's membership inside a FlatOccupancy episode. Own
/// JoinedDate/LeftDate let a family member join or leave mid-episode
/// without closing the whole group; EndOccupancyCommand bulk-sets LeftDate
/// on every open member when the whole episode closes.</summary>
public class OccupancyMember : BaseAuditableEntity
{
    public int FlatOccupancyId { get; set; }
    public FlatOccupancy FlatOccupancy { get; set; } = default!;

    public int PersonId { get; set; }
    public Person Person { get; set; } = default!;

    public PersonRelationship Relationship { get; set; }

    /// <summary>Primary Owner (Owner-type episodes) or Primary Tenant
    /// (Tenant-type episodes) — at most one true per episode, unless
    /// OccupancySettings.AllowMultiplePrimaryOwners for Owner-type.</summary>
    public bool IsPrimary { get; set; }

    public ResidentStatus ResidentStatus { get; set; } = ResidentStatus.Residing;

    public DateTime JoinedDate { get; set; }

    /// <summary>Null = still part of this episode.</summary>
    public DateTime? LeftDate { get; set; }
}
