using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

public class EmergencyContact : BaseAuditableEntity
{
    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public string ContactName { get; set; } = default!;

    public string Relationship { get; set; } = default!;

    public string Phone { get; set; } = default!;

    public string? AlternatePhone { get; set; }
}
