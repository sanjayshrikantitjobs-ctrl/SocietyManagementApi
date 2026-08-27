using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SocietyManagement.Application.Common.Models;
using SocietyManagement.Infrastructure.Services;
using Xunit;

namespace SocietyManagement.Tests.Vehicles;

/// <summary>Diagnostic-only: renders a sample festival receipt to a PNG for
/// visual inspection of the template overlay positioning. Not a real
/// assertion-based test — delete once positioning is confirmed correct.</summary>
public class ReceiptPreviewTests
{
    [Fact]
    public void GenerateSamplePreview()
    {
        var outDir = Environment.GetEnvironmentVariable("RECEIPT_PREVIEW_OUTDIR");
        if (string.IsNullOrWhiteSpace(outDir)) return;
        Directory.CreateDirectory(outDir);

        QuestPDF.Settings.License = LicenseType.Community;

        var data = new ContributionReceiptData(
            ReceiptNumber: "001234",
            SocietyName: "NAVYUG CO-OPERATIVE HOUSING SOCIETY LTD.",
            SocietyAddress: "123, Azad Nagar, Mumbai, Maharashtra - 400053",
            SocietyLogoUrl: null,
            FestivalName: "GANPATI & DURGA PUJA",
            FestivalYear: 2026,
            DonorName: "RAJESH K. SHARMA",
            FlatNumber: "A-304 (Wing A)",
            Amount: 2501m,
            PaymentMethod: "UPI (GPay/PhonePe)",
            PaymentDate: new DateTime(2026, 10, 22),
            TransactionId: "4298150376",
            TargetAmount: 5000m,
            TotalPaidForFlat: 2501m);

        var document = PdfReceiptService.BuildContributionReceiptDocument(data);
        var images = document.GenerateImages(new ImageGenerationSettings { RasterDpi = 200 });
        var pngBytes = images.First();

        File.WriteAllBytes(Path.Combine(outDir, "sample_receipt.png"), pngBytes);

        var fullyPaidData = data with { TargetAmount = 2501m };
        var fullyPaidPng = PdfReceiptService.BuildContributionReceiptDocument(fullyPaidData)
            .GenerateImages(new ImageGenerationSettings { RasterDpi = 200 }).First();
        File.WriteAllBytes(Path.Combine(outDir, "sample_receipt_fullypaid.png"), fullyPaidPng);
    }
}
