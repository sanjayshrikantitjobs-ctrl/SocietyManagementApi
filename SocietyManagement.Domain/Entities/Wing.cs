using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

public class Wing : BaseAuditableEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = default!;

    public string Name { get; set; } = default!;

    public ICollection<Floor> Floors { get; set; } = new List<Floor>();
}
