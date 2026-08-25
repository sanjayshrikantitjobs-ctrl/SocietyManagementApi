namespace SocietyManagement.Application.Common.Models;

/// <summary>Everything IPdfReceiptService needs to render a generic Finance
/// receipt (Maintenance/WaterTanker payments) — simpler than
/// ContributionReceiptData since there's no partial-payment/target concept
/// outside Festivals.</summary>
public record FinanceReceiptData(
    string ReceiptNumber,
    string SocietyName,
    string? SocietyLogoUrl,
    string SourceLabel,
    string PayerName,
    string? FlatNumber,
    decimal Amount,
    string? PaymentMethod,
    DateTime PaymentDate,
    string Description);
