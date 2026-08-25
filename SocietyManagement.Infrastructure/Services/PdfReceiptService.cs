using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>QuestPDF-based implementation of IPdfReceiptService (Community
/// license — free; see DependencyInjection.cs for the one-time license
/// registration this requires at startup).</summary>
public class PdfReceiptService : IPdfReceiptService
{
    public byte[] GenerateContributionReceipt(ContributionReceiptData data)
    {
        // SocietyLogoUrl is a "/uploads/..." web path (see LocalFileStorageService)
        // — resolve it against the same AppContext.BaseDirectory/wwwroot root
        // that path was originally saved under, since this service has no
        // access to the HTTP request context to resolve it any other way.
        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(data.SocietyLogoUrl))
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", data.SocietyLogoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(logoPath))
            {
                logoBytes = File.ReadAllBytes(logoPath);
            }
        }

        var isPartiallyPaid = data.TargetAmount.HasValue && data.TotalPaidForFlat < data.TargetAmount.Value;
        var remaining = data.TargetAmount.HasValue ? data.TargetAmount.Value - data.TotalPaidForFlat : 0m;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(headerColumn =>
                {
                    headerColumn.Item().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text(data.SocietyName).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                            column.Item().Text("Donation Receipt").FontSize(13).SemiBold();
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
                    column.Spacing(8);

                    void Row(string label, string value)
                    {
                        column.Item().Row(row =>
                        {
                            row.ConstantItem(120).Text(label).SemiBold();
                            row.RelativeItem().Text(value);
                        });
                    }

                    Row("Receipt No.", data.ReceiptNumber);
                    Row("Festival", data.FestivalName);
                    Row("Donor Name", data.DonorName);
                    if (!string.IsNullOrWhiteSpace(data.FlatNumber)) Row("Flat", data.FlatNumber);
                    Row("Amount Paid", $"Rs. {data.Amount:N2}");
                    Row("Payment Method", data.PaymentMethod);
                    Row("Payment Date", data.PaymentDate.ToString("dd MMM yyyy"));
                    if (!string.IsNullOrWhiteSpace(data.TransactionId)) Row("Transaction ID", data.TransactionId);

                    if (data.TargetAmount.HasValue)
                    {
                        column.Item().PaddingTop(8).Background(isPartiallyPaid ? Colors.Orange.Lighten4 : Colors.Green.Lighten4)
                            .Padding(10).Column(status =>
                            {
                                status.Spacing(4);
                                status.Item().Text(isPartiallyPaid ? "Partially Paid" : "Fully Paid")
                                    .Bold().FontColor(isPartiallyPaid ? Colors.Orange.Darken3 : Colors.Green.Darken3);
                                status.Item().Text($"Target: Rs. {data.TargetAmount.Value:N2}    Paid to date: Rs. {data.TotalPaidForFlat:N2}");
                                if (isPartiallyPaid)
                                {
                                    status.Item().Text($"Remaining: Rs. {remaining:N2}").SemiBold().FontColor(Colors.Orange.Darken3);
                                }
                            });
                    }
                });

                page.Footer().AlignCenter().Text(
                    "This is a system-generated receipt and does not require a signature.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }
}
