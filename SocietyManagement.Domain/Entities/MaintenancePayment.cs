using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class MaintenancePayment : BaseAuditableEntity
{
    public int MaintenanceBillId { get; set; }
    public MaintenanceBill MaintenanceBill { get; set; } = default!;

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public PaymentMode PaymentMode { get; set; }

    public string? TransactionReference { get; set; }

    public int? ReceivedByUserId { get; set; }

    public string? Notes { get; set; }
}
