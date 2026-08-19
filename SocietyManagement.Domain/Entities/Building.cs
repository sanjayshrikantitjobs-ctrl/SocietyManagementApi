using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

public class Building : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public ICollection<Wing> Wings { get; set; } = new List<Wing>();
}
