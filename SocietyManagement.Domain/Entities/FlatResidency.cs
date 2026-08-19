using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>Join between Member and Flat carrying a role and a time period —
/// this one table is the source of truth for "is this flat rented, by
/// whom", "how many people live here", and "history of flat owners", all
/// answered by querying rows rather than storing redundant fields.</summary>
public class FlatResidency : BaseAuditableEntity
{
    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = default!;

    public MemberType MemberType { get; set; }

    public DateTime MoveInDate { get; set; }

    /// <summary>Null = currently resides here.</summary>
    public DateTime? MoveOutDate { get; set; }

    /// <summary>The household's main contact for this period.</summary>
    public bool IsPrimaryContact { get; set; }

    public string? Notes { get; set; }
}
