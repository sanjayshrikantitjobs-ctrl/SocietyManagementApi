using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

public class VisitorPurpose : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>When false, a visit with this purpose skips resident approval
    /// entirely and goes straight to Approved on creation.</summary>
    public bool RequiresApproval { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
