using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

public class Floor : BaseAuditableEntity
{
    public int WingId { get; set; }
    public Wing Wing { get; set; } = default!;

    public int FloorNumber { get; set; }

    public string? Name { get; set; }

    public ICollection<Flat> Flats { get; set; } = new List<Flat>();
}
