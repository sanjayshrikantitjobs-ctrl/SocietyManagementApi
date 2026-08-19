namespace SocietyManagement.Application.Common.Models;

/// <summary>Everything IPdfReceiptService needs to render a contribution
/// receipt, kept independent of the EF entity shape.</summary>
public record ContributionReceiptData(
    string ReceiptNumber,
    string SocietyName,
    string FestivalName,
    string DonorName,
    string? FlatNumber,
    decimal Amount,
    string PaymentMethod,
    DateTime PaymentDate,
    string? TransactionId);
