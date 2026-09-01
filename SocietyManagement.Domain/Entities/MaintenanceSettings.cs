using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One row per society — billing generation/due-date/late-fee rules,
/// invoice numbering, and the configurable bill footer / WhatsApp template.</summary>
public class MaintenanceSettings : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    /// <summary>Day of month (1-28) bills are auto-generated on.</summary>
    public int BillGenerationDay { get; set; } = 1;

    /// <summary>Day of month a bill is due by.</summary>
    public int DueDay { get; set; } = 10;

    /// <summary>Days after DueDay before the late fee applies (0 = immediately after due date).</summary>
    public int GracePeriodDays { get; set; }

    public decimal LateFeeAmount { get; set; }

    public string InvoiceNumberPrefix { get; set; } = "INV";

    /// <summary>Next sequence number to allocate; incremented on every bill generated.</summary>
    public int NextInvoiceNumber { get; set; } = 1;

    /// <summary>Placeholders: {OwnerName} {Month} {Amount} {DueDate}.</summary>
    public string WhatsAppMessageTemplate { get; set; } =
        "Dear {OwnerName},\nYour Maintenance Bill for {Month} has been generated.\nAmount: {Amount}\nDue Date: {DueDate}\nPlease find attached invoice.";

    public string PdfFooterMessage { get; set; } =
        "Thank you for contributing towards the maintenance of our society.";

    /// <summary>Super-Admin-only switch (see UpsertMaintenanceSettingsCommandHandler)
    /// — gates both the automatic send on bill generation and the manual
    /// "Resend WhatsApp" action. Defaults true so existing societies keep
    /// today's always-on behavior until a Super Admin explicitly disables it.</summary>
    public bool WhatsAppEnabled { get; set; } = true;
}
