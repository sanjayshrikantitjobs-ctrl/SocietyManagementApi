using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>QuestPDF-based maintenance bill invoice — mirrors PdfReceiptService's
/// pattern (Community license, registered once in Program.cs) but as a full A4
/// invoice: logo, itemized table, grand total, payment instructions, QR
/// placeholder, and the admin-configurable footer.</summary>
public class MaintenanceBillPdfService : IMaintenanceBillPdfService
{
    public byte[] GenerateBillPdf(MaintenanceBillPdfData data)
    {
        var logoBytes = TryLoadLogo(data.SocietyLogoUrl);
        var grandTotal = data.Items.Sum(i => i.Amount) + data.PreviousBalance + data.FineAmount;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        if (logoBytes is not null)
                        {
                            row.ConstantItem(50).Height(50).Image(logoBytes).FitArea();
                        }
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text(data.SocietyName).FontSize(16).Bold();
                            inner.Item().Text(data.SocietyAddress).FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(140).AlignRight().Column(inner =>
                        {
                            inner.Item().Text("MAINTENANCE BILL").FontSize(12).Bold();
                            inner.Item().Text($"Invoice: {data.InvoiceNumber}").FontSize(9);
                            inner.Item().Text($"Month: {data.BillMonthLabel}").FontSize(9);
                        });
                    });
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Flat: {data.FlatNumber}").Bold();
                        row.RelativeItem().AlignRight().Text($"Owner: {data.OwnerName ?? "-"}");
                    });
                    column.Item().Text($"Due Date: {data.DueDate:dd MMM yyyy}").FontColor(Colors.Red.Darken1).SemiBold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Description").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("Amount (Rs.)").Bold();
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(item.Description);
                            table.Cell().Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.Amount:N2}");
                        }

                        if (data.PreviousBalance > 0)
                        {
                            table.Cell().Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text("Previous Balance");
                            table.Cell().Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{data.PreviousBalance:N2}");
                        }

                        if (data.FineAmount > 0)
                        {
                            table.Cell().Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text("Fine");
                            table.Cell().Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{data.FineAmount:N2}");
                        }

                        table.Cell().Padding(6).Background(Colors.Grey.Lighten3).Text("Grand Total").Bold();
                        table.Cell().Padding(6).Background(Colors.Grey.Lighten3).AlignRight().Text($"Rs. {grandTotal:N2}").Bold();
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text("Payment Instructions").Bold().FontSize(10);
                            inner.Item().Text("Pay by Cash, UPI or Bank Transfer to the society office. " +
                                "Share the transaction reference with the accountant to update your bill.").FontSize(9);
                        });
                        row.ConstantItem(90).Height(90).Border(1).BorderColor(Colors.Grey.Lighten1)
                            .AlignCenter().AlignMiddle()
                            .Text("QR Code\n(Online Payment\nComing Soon)").FontSize(7).AlignCenter().FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Footer().AlignCenter().Text(data.FooterMessage).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }

    private static byte[]? TryLoadLogo(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl) || !logoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var relativePath = logoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath);
            return File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : null;
        }
        catch
        {
            return null;
        }
    }
}
