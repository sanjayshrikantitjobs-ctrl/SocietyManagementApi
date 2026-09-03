namespace SocietyManagement.Application.Common.Models;

public class MaintenanceBillExportRow
{
    public string FlatNumber { get; set; } = default!;
    public string BuildingName { get; set; } = default!;
    public string WingName { get; set; } = default!;
    public string? OwnerName { get; set; }
    public string? TenantName { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public DateTime BillMonth { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public DateTime DueDate { get; set; }
    public string StatusLabel { get; set; } = default!;
}

public class MaintenanceBillsExportData
{
    public string SocietyName { get; set; } = default!;
    public string FilterLabel { get; set; } = default!;
    public List<MaintenanceBillExportRow> Rows { get; set; } = new();
}
