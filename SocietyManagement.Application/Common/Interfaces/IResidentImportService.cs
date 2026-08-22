using SocietyManagement.Application.Features.Residents;

namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Abstraction over the Excel (.xlsx) file format so Application
/// handlers don't reference a concrete spreadsheet library (ClosedXML lives
/// in Infrastructure) — same split as IPdfReceiptService/QuestPDF.</summary>
public interface IResidentImportService
{
    /// <summary>Parses every data row (row 1 is the header) into a raw DTO —
    /// no DB validation here, just reading cells into strings/numbers.</summary>
    List<ResidentImportRowDto> ParseRows(byte[] fileContent);

    /// <summary>A starter .xlsx with the exact expected headers, one example
    /// row, and a legend of valid Flat Type / Member Type text values.</summary>
    byte[] GenerateTemplate();
}
