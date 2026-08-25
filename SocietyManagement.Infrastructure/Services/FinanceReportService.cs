using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>QuestPDF + ClosedXML implementation of IFinanceReportService.
/// Both libraries are already referenced in this project (QuestPDF via
/// PdfReceiptService/MaintenanceBillPdfService, ClosedXML via
/// ClosedXmlResidentImportService for imports) — this is the first
/// write-side use of ClosedXML.</summary>
public class FinanceReportService : IFinanceReportService
{
    public byte[] GeneratePdf(FinanceReportData data)
    {
        var periodLabel = data.DateFrom.HasValue || data.DateTo.HasValue
            ? $"{data.DateFrom?.ToString("dd MMM yyyy") ?? "Inception"} - {data.DateTo?.ToString("dd MMM yyyy") ?? "Date"}"
            : "All Time";

        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(data.SocietyLogoUrl))
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", data.SocietyLogoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(logoPath))
            {
                logoBytes = File.ReadAllBytes(logoPath);
            }
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(headerColumn =>
                {
                    headerColumn.Item().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text(data.SocietyName).FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                            column.Item().Text("Financial Report").FontSize(14).SemiBold();
                            column.Item().Text($"Period: {periodLabel}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                        if (logoBytes is not null)
                        {
                            row.ConstantItem(60).Height(60).Image(logoBytes).FitArea();
                        }
                    });
                    headerColumn.Item().PaddingTop(8).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
                });

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(15);

                    column.Item().Row(row =>
                    {
                        void SummaryCard(string label, decimal amount, string color)
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                            {
                                c.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"Rs. {amount:N2}").FontSize(14).Bold().FontColor(color);
                            });
                        }
                        SummaryCard("Total Income", data.TotalIncome, Colors.Green.Darken2);
                        SummaryCard("Total Expense", data.TotalExpense, Colors.Red.Darken2);
                        SummaryCard("Net Balance", data.NetBalance, data.NetBalance >= 0 ? Colors.Blue.Darken2 : Colors.Red.Darken2);
                    });

                    // Row-based layout, left-aligned amounts. Table(), ratio-
                    // based RelativeItem columns, and ConstantItem+AlignRight
                    // all clipped digits off the Amount column in testing
                    // (confirmed against the actual downloaded PDF, not just
                    // code review) — left-aligned ConstantItem is the one
                    // combination that rendered every character correctly.
                    void Section(string title, List<FinanceReportLine> lines)
                    {
                        column.Item().Text(title).FontSize(12).Bold();
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Category").SemiBold();
                            row.ConstantItem(160).Text("Amount").SemiBold();
                        });
                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        foreach (var line in lines)
                        {
                            column.Item().PaddingVertical(3).Row(row =>
                            {
                                row.RelativeItem().Text(line.Label);
                                row.ConstantItem(160).Text($"Rs. {line.Amount:N2}");
                            });
                        }
                    }

                    Section("Income by Source", data.IncomeBySource);
                    Section("Expense by Category", data.ExpenseByCategory);
                });

                page.Footer().AlignCenter().Text(
                    $"Generated on {DateTime.Now:dd MMM yyyy HH:mm} - system-generated report.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateExcel(FinanceReportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Financial Report");

        sheet.Cell(1, 1).Value = data.SocietyName;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = "Financial Report";
        var periodLabel = data.DateFrom.HasValue || data.DateTo.HasValue
            ? $"{data.DateFrom?.ToString("dd MMM yyyy") ?? "Inception"} - {data.DateTo?.ToString("dd MMM yyyy") ?? "Date"}"
            : "All Time";
        sheet.Cell(3, 1).Value = $"Period: {periodLabel}";

        var row = 5;
        sheet.Cell(row, 1).Value = "Total Income";
        sheet.Cell(row, 2).Value = data.TotalIncome;
        row++;
        sheet.Cell(row, 1).Value = "Total Expense";
        sheet.Cell(row, 2).Value = data.TotalExpense;
        row++;
        sheet.Cell(row, 1).Value = "Net Balance";
        sheet.Cell(row, 2).Value = data.NetBalance;
        sheet.Range(row - 2, 1, row, 2).Style.Font.Bold = true;
        row += 2;

        void WriteSection(string title, List<FinanceReportLine> lines)
        {
            sheet.Cell(row, 1).Value = title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
            sheet.Cell(row, 1).Value = "Category";
            sheet.Cell(row, 2).Value = "Amount";
            sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
            row++;
            foreach (var line in lines)
            {
                sheet.Cell(row, 1).Value = line.Label;
                sheet.Cell(row, 2).Value = line.Amount;
                row++;
            }
            row++;
        }

        WriteSection("Income by Source", data.IncomeBySource);
        WriteSection("Expense by Category", data.ExpenseByCategory);

        sheet.Columns(1, 2).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
