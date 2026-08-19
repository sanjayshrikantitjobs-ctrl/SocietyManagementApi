using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Abstraction over PDF generation so Application handlers don't
/// reference a concrete PDF library (QuestPDF lives in Infrastructure).</summary>
public interface IPdfReceiptService
{
    byte[] GenerateContributionReceipt(ContributionReceiptData data);
}
