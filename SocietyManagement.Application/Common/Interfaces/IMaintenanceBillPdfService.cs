using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Application.Common.Interfaces;

public interface IMaintenanceBillPdfService
{
    byte[] GenerateBillPdf(MaintenanceBillPdfData data);
}
