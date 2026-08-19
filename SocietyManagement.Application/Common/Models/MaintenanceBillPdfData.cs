namespace SocietyManagement.Application.Common.Models;

public record MaintenanceBillPdfItem(string Description, decimal Amount);

/// <summary>Everything IMaintenanceBillPdfService needs to render an invoice,
/// kept independent of the EF entity shape.</summary>
public record MaintenanceBillPdfData(
    string SocietyName,
    string SocietyAddress,
    string? SocietyLogoUrl,
    string InvoiceNumber,
    string BillMonthLabel,
    string FlatNumber,
    string? OwnerName,
    IReadOnlyList<MaintenanceBillPdfItem> Items,
    decimal PreviousBalance,
    decimal FineAmount,
    decimal TotalAmount,
    DateTime DueDate,
    string FooterMessage);
