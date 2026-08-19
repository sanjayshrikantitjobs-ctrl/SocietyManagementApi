using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A recurring maintenance charge line (Maintenance, Water Tanker,
/// Lift, Security, ...) that gets included on every flat's monthly bill.</summary>
public class MaintenanceCategory : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string ChargeName { get; set; } = default!;

    public ChargeType ChargeType { get; set; }

    public decimal MonthlyAmount { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public ICollection<MaintenanceBillItem> BillItems { get; set; } = new List<MaintenanceBillItem>();
}
