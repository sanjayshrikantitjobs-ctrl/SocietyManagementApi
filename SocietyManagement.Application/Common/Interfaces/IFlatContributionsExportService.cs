using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Multi-row "By Flat" contribution export (target/paid/outstanding
/// per flat, whatever Status filter is active) — separate from the
/// single-contribution receipt PDF.</summary>
public interface IFlatContributionsExportService
{
    byte[] GeneratePdf(FlatContributionsExportData data);
    byte[] GenerateExcel(FlatContributionsExportData data);
}
