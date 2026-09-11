using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>QuestPDF + ClosedXML implementation of IFlatContributionsExportService
/// — same pairing as MaintenanceBillsExportService.</summary>
public class FlatContributionsExportService : IFlatContributionsExportService
{
    public byte[] GeneratePdf(FlatContributionsExportData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(headerColumn =>
                {
                    headerColumn.Item().Text(data.SocietyName).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                    headerColumn.Item().Text($"{data.FestivalName} — Contributions by Flat").FontSize(12).SemiBold();
                    headerColumn.Item().Text(data.FilterLabel).FontSize(9).FontColor(Colors.Grey.Darken1);
                    headerColumn.Item().PaddingTop(6).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1f);   // Flat
                        columns.RelativeColumn(1.3f); // Target
                        columns.RelativeColumn(1.3f); // Paid
                        columns.RelativeColumn(1.3f); // Outstanding
                        columns.RelativeColumn(1.4f); // Payment Method
                        columns.RelativeColumn(1.3f); // Status
                    });

                    void Header(string text) => table.Cell().Background(Colors.Blue.Darken2).Padding(5)
                        .Text(text).FontColor(Colors.White).SemiBold();
                    Header("Flat");
                    Header("Target");
                    Header("Paid");
                    Header("Outstanding");
                    Header("Payment Method");
                    Header("Status");

                    var alternate = false;
                    foreach (var row in data.Rows)
                    {
                        var bg = alternate ? Colors.Grey.Lighten4 : Colors.White;
                        alternate = !alternate;

                        void Cell(string text) => table.Cell().Background(bg).Padding(5).Text(text);
                        Cell(row.FlatNumber);
                        Cell($"Rs. {row.TargetAmount:N0}");
                        Cell($"Rs. {row.PaidAmount:N0}");
                        Cell($"Rs. {row.OutstandingAmount:N0}");
                        Cell(row.LastPaymentMethodLabel ?? "-");
                        Cell(row.StatusLabel + (row.DeclineReason is null ? "" : $" (Declined: {row.DeclineReason})"));
                    }
                });

                page.Footer().AlignCenter().Text(
                    $"Generated on {DateTime.Now:dd MMM yyyy HH:mm} — {data.Rows.Count} flat(s).")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateExcel(FlatContributionsExportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Contributions by Flat");

        sheet.Cell(1, 1).Value = data.SocietyName;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"{data.FestivalName} — Contributions by Flat";
        sheet.Cell(3, 1).Value = data.FilterLabel;

        var headerRow = 5;
        string[] headers = ["Flat", "Target", "Paid", "Outstanding", "Payment Method", "Status", "Decline Reason"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(headerRow, i + 1).Value = headers[i];
        }
        sheet.Range(headerRow, 1, headerRow, headers.Length).Style.Font.Bold = true;

        var row = headerRow + 1;
        foreach (var r in data.Rows)
        {
            sheet.Cell(row, 1).Value = r.FlatNumber;
            sheet.Cell(row, 2).Value = r.TargetAmount;
            sheet.Cell(row, 3).Value = r.PaidAmount;
            sheet.Cell(row, 4).Value = r.OutstandingAmount;
            sheet.Cell(row, 5).Value = r.LastPaymentMethodLabel ?? "";
            sheet.Cell(row, 6).Value = r.StatusLabel;
            sheet.Cell(row, 7).Value = r.DeclineReason ?? "";
            row++;
        }

        sheet.Columns(1, headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
