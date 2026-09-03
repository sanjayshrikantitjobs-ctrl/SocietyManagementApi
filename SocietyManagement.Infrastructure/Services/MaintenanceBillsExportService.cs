using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>QuestPDF + ClosedXML implementation of IMaintenanceBillsExportService
/// — both libraries already referenced (see FinanceReportService for the same
/// pairing on a different export).</summary>
public class MaintenanceBillsExportService : IMaintenanceBillsExportService
{
    public byte[] GeneratePdf(MaintenanceBillsExportData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(headerColumn =>
                {
                    headerColumn.Item().Text(data.SocietyName).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                    headerColumn.Item().Text("Maintenance Bills").FontSize(12).SemiBold();
                    headerColumn.Item().Text(data.FilterLabel).FontSize(9).FontColor(Colors.Grey.Darken1);
                    headerColumn.Item().PaddingTop(6).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.2f); // Flat
                        columns.RelativeColumn(2f);   // Owner/Tenant
                        columns.RelativeColumn(1.6f); // Invoice
                        columns.RelativeColumn(1.2f); // Month
                        columns.RelativeColumn(1.1f); // Total
                        columns.RelativeColumn(1.1f); // Paid
                        columns.RelativeColumn(1.1f); // Balance
                        columns.RelativeColumn(1.2f); // Due Date
                        columns.RelativeColumn(1.1f); // Status
                    });

                    void Header(string text) => table.Cell().Background(Colors.Blue.Darken2).Padding(5)
                        .Text(text).FontColor(Colors.White).SemiBold();
                    Header("Flat");
                    Header("Owner / Tenant");
                    Header("Invoice");
                    Header("Month");
                    Header("Total");
                    Header("Paid");
                    Header("Balance");
                    Header("Due Date");
                    Header("Status");

                    var alternate = false;
                    foreach (var row in data.Rows)
                    {
                        var bg = alternate ? Colors.Grey.Lighten4 : Colors.White;
                        alternate = !alternate;

                        void Cell(string text) => table.Cell().Background(bg).Padding(5).Text(text);
                        Cell($"{row.FlatNumber}\n{row.BuildingName}/{row.WingName}");
                        Cell(row.OwnerName ?? row.TenantName ?? "-");
                        Cell(row.InvoiceNumber);
                        Cell(row.BillMonth.ToString("MMM yyyy"));
                        Cell($"Rs. {row.TotalAmount:N0}");
                        Cell($"Rs. {row.AmountPaid:N0}");
                        Cell($"Rs. {row.Balance:N0}");
                        Cell(row.DueDate.ToString("dd MMM yyyy"));
                        Cell(row.StatusLabel);
                    }
                });

                page.Footer().AlignCenter().Text(
                    $"Generated on {DateTime.Now:dd MMM yyyy HH:mm} — {data.Rows.Count} bill(s).")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateExcel(MaintenanceBillsExportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Maintenance Bills");

        sheet.Cell(1, 1).Value = data.SocietyName;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = "Maintenance Bills";
        sheet.Cell(3, 1).Value = data.FilterLabel;

        var headerRow = 5;
        string[] headers = ["Flat", "Building / Wing", "Owner", "Tenant", "Invoice", "Bill Month", "Total", "Paid", "Balance", "Due Date", "Status"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(headerRow, i + 1).Value = headers[i];
        }
        sheet.Range(headerRow, 1, headerRow, headers.Length).Style.Font.Bold = true;

        var row = headerRow + 1;
        foreach (var r in data.Rows)
        {
            sheet.Cell(row, 1).Value = r.FlatNumber;
            sheet.Cell(row, 2).Value = $"{r.BuildingName} / {r.WingName}";
            sheet.Cell(row, 3).Value = r.OwnerName ?? "";
            sheet.Cell(row, 4).Value = r.TenantName ?? "";
            sheet.Cell(row, 5).Value = r.InvoiceNumber;
            sheet.Cell(row, 6).Value = r.BillMonth.ToString("MMMM yyyy");
            sheet.Cell(row, 7).Value = r.TotalAmount;
            sheet.Cell(row, 8).Value = r.AmountPaid;
            sheet.Cell(row, 9).Value = r.Balance;
            sheet.Cell(row, 10).Value = r.DueDate.ToString("dd MMM yyyy");
            sheet.Cell(row, 11).Value = r.StatusLabel;
            row++;
        }

        sheet.Columns(1, headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
