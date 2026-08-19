using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class FineRecord : BaseAuditableEntity
{
    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public string Reason { get; set; } = default!;

    public decimal Amount { get; set; }

    public DateTime FineDate { get; set; }

    public FineStatus Status { get; set; } = FineStatus.Pending;

    public ICollection<MaintenanceBillItem> BillItems { get; set; } = new List<MaintenanceBillItem>();
}
