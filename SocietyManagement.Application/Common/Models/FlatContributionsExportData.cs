namespace SocietyManagement.Application.Common.Models;

public class FlatContributionExportRow
{
    public string FlatNumber { get; set; } = default!;
    public decimal TargetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string StatusLabel { get; set; } = default!;
    public string? LastPaymentMethodLabel { get; set; }
    public string? DeclineReason { get; set; }
}

public class FlatContributionsExportData
{
    public string SocietyName { get; set; } = default!;
    public string FestivalName { get; set; } = default!;
    public string FilterLabel { get; set; } = default!;
    public List<FlatContributionExportRow> Rows { get; set; } = new();
}
