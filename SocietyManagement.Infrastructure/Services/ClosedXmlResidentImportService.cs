using ClosedXML.Excel;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Residents;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>ClosedXML-backed reader/writer for the Resident Import
/// spreadsheet. Column order is fixed (A..M) and must stay in sync with
/// GenerateTemplate() below — ParseRows reads by position, not header text,
/// so a re-ordered template would silently misread columns; the header row
/// is written and skipped but never matched against.</summary>
public class ClosedXmlResidentImportService : IResidentImportService
{
    private const int HeaderRow = 1;
    private const int FirstDataRow = 2;

    public List<ResidentImportRowDto> ParseRows(byte[] fileContent)
    {
        using var stream = new MemoryStream(fileContent);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        var rows = new List<ResidentImportRowDto>();
        var rowNumber = FirstDataRow;

        while (true)
        {
            var building = sheet.Cell(rowNumber, 1).GetString().Trim();
            var flatNumber = sheet.Cell(rowNumber, 4).GetString().Trim();
            if (string.IsNullOrWhiteSpace(building) && string.IsNullOrWhiteSpace(flatNumber))
            {
                break; // first fully-blank row = end of data
            }

            rows.Add(new ResidentImportRowDto
            {
                RowNumber = rowNumber,
                Building = building,
                Wing = sheet.Cell(rowNumber, 2).GetString().Trim(),
                FloorNumber = TryGetInt(sheet.Cell(rowNumber, 3)),
                FlatNumber = flatNumber,
                FlatTypeLabel = sheet.Cell(rowNumber, 5).GetString().Trim(),
                AreaSqFt = TryGetDecimal(sheet.Cell(rowNumber, 6)),
                IsVacant = IsYes(sheet.Cell(rowNumber, 7).GetString()),
                OwnerFirstName = sheet.Cell(rowNumber, 8).GetString().Trim(),
                OwnerLastName = sheet.Cell(rowNumber, 9).GetString().Trim(),
                OwnerPhone = sheet.Cell(rowNumber, 10).GetString().Trim(),
                OwnerEmail = sheet.Cell(rowNumber, 11).GetString().Trim(),
                MemberTypeLabel = sheet.Cell(rowNumber, 12).GetString().Trim(),
                MoveInDate = TryGetDate(sheet.Cell(rowNumber, 13))
            });

            rowNumber++;
        }

        return rows;
    }

    public byte[] GenerateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Residents");

        string[] headers =
        {
            "Building", "Wing (optional)", "Floor Number", "Flat Number", "Flat Type",
            "Area (sq ft)", "Vacant (Yes/No)", "Owner First Name", "Owner Last Name",
            "Owner Phone", "Owner Email (optional)", "Member Type (Owner/Tenant)", "Move-In Date (optional)"
        };
        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(HeaderRow, col + 1).Value = headers[col];
        }
        sheet.Row(HeaderRow).Style.Font.SetBold();

        // Example row — left as sample data the admin overwrites, since a
        // fully blank template gives no hint of the expected value formats
        // (Flat Type / Member Type text, Yes/No for Vacant).
        var example = new object?[]
        {
            "Ambesh Tower-1", "", 1, "101", "1 BHK", 950, "No",
            "Rohan", "Sharma", "9876543210", "rohan@example.com", "Owner", DateTime.Today
        };
        for (var col = 0; col < example.Length; col++)
        {
            sheet.Cell(FirstDataRow, col + 1).Value = XLCellValue.FromObject(example[col]);
        }

        sheet.Columns().AdjustToContents();

        var legend = workbook.Worksheets.Add("Legend");
        legend.Cell(1, 1).Value = "Flat Type values";
        legend.Cell(1, 1).Style.Font.SetBold();
        string[] flatTypes = { "1 RK", "1 BHK", "2 BHK", "3 BHK", "4 BHK", "Duplex", "Penthouse", "Shop", "Office" };
        for (var i = 0; i < flatTypes.Length; i++) legend.Cell(i + 2, 1).Value = flatTypes[i];

        legend.Cell(1, 3).Value = "Member Type values";
        legend.Cell(1, 3).Style.Font.SetBold();
        legend.Cell(2, 3).Value = "Owner";
        legend.Cell(3, 3).Value = "Tenant";

        legend.Cell(1, 5).Value = "Notes";
        legend.Cell(1, 5).Style.Font.SetBold();
        legend.Cell(2, 5).Value = "Leave Owner columns blank and set Vacant to Yes for an empty flat.";
        legend.Cell(3, 5).Value = "Wing can be left blank for a single-wing building.";
        legend.Cell(4, 5).Value = "Building/Wing/Floor are created automatically if they don't already exist.";
        legend.Columns().AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static int TryGetInt(IXLCell cell) => cell.TryGetValue(out int value) ? value : 0;

    private static decimal? TryGetDecimal(IXLCell cell) => cell.TryGetValue(out decimal value) ? value : null;

    private static DateTime? TryGetDate(IXLCell cell)
    {
        if (cell.TryGetValue(out DateTime date)) return date;
        var text = cell.GetString().Trim();
        return !string.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, out var parsed) ? parsed : null;
    }

    private static bool IsYes(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals("Yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("True", StringComparison.OrdinalIgnoreCase)
            || normalized == "1";
    }
}
