using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A resident-raised (or admin-raised-on-behalf-of) complaint,
/// moving through a linear Open → Assigned → InProgress → Resolved →
/// Closed workflow. Each transition's timestamp/actor lives directly on
/// this row (mirrors VisitorVisit) rather than a separate history table —
/// enough to show "who did what when" without extra plumbing.</summary>
public class Complaint : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    /// <summary>Always the authenticated caller — the resident for a
    /// self-raised complaint, or the admin for one raised on a resident's
    /// behalf.</summary>
    public int RaisedByUserId { get; set; }

    public string RaisedByName { get; set; } = default!;

    public ComplaintCategory Category { get; set; }

    public string Title { get; set; } = default!;

    public string Description { get; set; } = default!;

    public ComplaintPriority Priority { get; set; }

    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

    public string? PhotoUrl { get; set; }

    public int? AssignedStaffId { get; set; }
    public Staff? AssignedStaff { get; set; }

    public DateTime? AssignedAt { get; set; }
    public int? AssignedByUserId { get; set; }

    public DateTime? InProgressAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }

    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }

    public string? ReopenReason { get; set; }
}
