using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One gate-entry attempt: a Visitor, at a Flat, via a Gate, for a
/// Purpose, moving through the approval/check-in/check-out lifecycle. The
/// inherited CreatedBy (BaseAuditableEntity) is a display string, not
/// usable for targeting a SignalR notification back at the creating
/// watchman — CreatedByUserId is the real FK for that.</summary>
public class VisitorVisit : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int VisitorId { get; set; }
    public Visitor Visitor { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public int PurposeId { get; set; }
    public VisitorPurpose Purpose { get; set; } = default!;

    public int GateId { get; set; }
    public Gate Gate { get; set; } = default!;

    public int NumberOfVisitors { get; set; } = 1;

    public VisitorVisitStatus Status { get; set; } = VisitorVisitStatus.PendingApproval;

    /// <summary>The watchman who created this request — the target for
    /// real-time approve/reject/expire notifications.</summary>
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public DateTime RequestedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public User? RejectedByUser { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime? CheckInTime { get; set; }
    public int? CheckedInByUserId { get; set; }
    public User? CheckedInByUser { get; set; }

    public DateTime? CheckOutTime { get; set; }
    public int? CheckedOutByUserId { get; set; }
    public User? CheckedOutByUser { get; set; }

    /// <summary>Set only while Status is PendingApproval — lets the WhatsApp
    /// approval link authorize approve/reject without a login, since a flat
    /// may have no resident user account at all. Not cleared once actioned;
    /// the Status guard (must still be PendingApproval) is what prevents a
    /// stale/reused link from doing anything.</summary>
    public string? ApprovalToken { get; set; }
}
