using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Multi-row Maintenance Bills export — separate from
/// IMaintenanceBillPdfService, which only ever renders a single invoice.</summary>
public interface IMaintenanceBillsExportService
{
    byte[] GeneratePdf(MaintenanceBillsExportData data);
    byte[] GenerateExcel(MaintenanceBillsExportData data);
}
