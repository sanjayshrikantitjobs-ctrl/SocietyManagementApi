using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A bug/support ticket raised against the software itself — by an
/// Admin or Member, for Super Admin (the software vendor's own role here)
/// to resolve. Deliberately separate from Complaint, which is a
/// society-internal issue (noise, maintenance, ...) scoped and resolved
/// within that one society.</summary>
public class SupportTicket : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;

    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }
    public string? ResolutionNotes { get; set; }
}
