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
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text(data.SocietyName).FontSize(16).Bold();
                    column.Item().Text("Donation Receipt").FontSize(13).SemiBold();
                    column.Item().PaddingTop(4).LineHorizontal(1);
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
                    Row("Amount", $"Rs. {data.Amount:N2}");
                    Row("Payment Method", data.PaymentMethod);
                    Row("Payment Date", data.PaymentDate.ToString("dd MMM yyyy"));
                    if (!string.IsNullOrWhiteSpace(data.TransactionId)) Row("Transaction ID", data.TransactionId);
                });

                page.Footer().AlignCenter().Text(
                    "This is a system-generated receipt and does not require a signature.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }
}
