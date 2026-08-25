using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Multi-row Financial Reports export — separate from
/// IPdfReceiptService, which only ever renders a single-record receipt.</summary>
public interface IFinanceReportService
{
    byte[] GeneratePdf(FinanceReportData data);
    byte[] GenerateExcel(FinanceReportData data);
}
