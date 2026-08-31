using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One flat's bill for one month. OwnerName/PhoneSnapshot capture
/// Flat.OwnerName/OwnerPhone at generation time — an invoice must not
/// silently change if the flat's owner record is edited later.</summary>
public class MaintenanceBill : BaseAuditableEntity
{
    /// <summary>Denormalized from Flat.Floor.Wing.Building.SocietyId at
    /// creation time — that 4-table join chain has no supporting index and
    /// doesn't scale once many societies share this table, so every query
    /// that used to walk it filters on this column directly instead.</summary>
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public DateTime BillMonth { get; set; }

    public string InvoiceNumber { get; set; } = default!;

    public decimal PreviousBalance { get; set; }

    public decimal FineAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal AmountPaid { get; set; }

    public DateTime DueDate { get; set; }

    public BillStatus Status { get; set; } = BillStatus.Pending;

    public string? PdfUrl { get; set; }

    public DateTime GeneratedAt { get; set; }

    public string? OwnerNameSnapshot { get; set; }

    public string? OwnerPhoneSnapshot { get; set; }

    public ICollection<MaintenanceBillItem> Items { get; set; } = new List<MaintenanceBillItem>();
    public ICollection<MaintenancePayment> Payments { get; set; } = new List<MaintenancePayment>();
}
