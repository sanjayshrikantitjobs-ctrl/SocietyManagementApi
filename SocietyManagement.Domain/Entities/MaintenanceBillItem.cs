using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class MaintenanceBillItem : BaseAuditableEntity
{
    public int MaintenanceBillId { get; set; }
    public MaintenanceBill MaintenanceBill { get; set; } = default!;

    public string Description { get; set; } = default!;

    public decimal Amount { get; set; }

    public BillItemType ItemType { get; set; }

    public int? MaintenanceCategoryId { get; set; }
    public MaintenanceCategory? MaintenanceCategory { get; set; }

    public int? SpecialChargeId { get; set; }
    public SpecialCharge? SpecialCharge { get; set; }

    public int? FineRecordId { get; set; }
    public FineRecord? FineRecord { get; set; }
}
