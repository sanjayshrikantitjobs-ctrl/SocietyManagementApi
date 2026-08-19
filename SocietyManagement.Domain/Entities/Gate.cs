using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

public class Gate : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}
