namespace SocietyManagement.Application.Common.Models;

/// <summary>Everything IPdfReceiptService needs to render a contribution
/// receipt, kept independent of the EF entity shape.</summary>
public record ContributionReceiptData(
    string ReceiptNumber,
    string SocietyName,
    string? SocietyLogoUrl,
    string FestivalName,
    string DonorName,
    string? FlatNumber,
    decimal Amount,
    string PaymentMethod,
    DateTime PaymentDate,
    string? TransactionId,
    /// <summary>Null when the flat has no target set for this festival
    /// (or the donation is anonymous/flat-less) — the receipt then shows no
    /// balance-due section at all.</summary>
    decimal? TargetAmount,
    /// <summary>Cumulative paid-to-date for this flat+festival, including
    /// this receipt's own payment.</summary>
    decimal TotalPaidForFlat);
