using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>QuestPDF-based implementation of IPdfReceiptService (Community
/// license — free; see DependencyInjection.cs for the one-time license
/// registration this requires at startup).</summary>
public class PdfReceiptService : IPdfReceiptService
{
    // The template is a fixed 1408x768 image (a user-generated festival receipt
    // background — decorative border, Ganpati/Durga artwork, static labels and
    // blank underlined fields already baked in). All coordinates below are in
    // that same pixel space; the canvas is scaled once so they can be used
    // directly without per-field conversion.
    private const float TemplateWidth = 1408f;
    private const float TemplateHeight = 768f;

    // Extra strip below the template image itself, for the "system-generated,
    // no signature required" disclaimer — the template has no room for it
    // without crowding "Thank You"/"Har Har Mahadev" or the decorative border.
    private const float FooterStripHeight = 42f;
    private const float CanvasHeight = TemplateHeight + FooterStripHeight;

    public byte[] GenerateContributionReceipt(ContributionReceiptData data) => BuildContributionReceiptDocument(data).GeneratePdf();

    /// <summary>Exposed separately (internal, not just private) so tests can
    /// call GenerateImages on it directly for visual verification of the
    /// template overlay positioning, without duplicating this whole method.</summary>
    internal static QuestPDF.Infrastructure.IDocument BuildContributionReceiptDocument(ContributionReceiptData data)
    {
        // Rendered as a single raster image via SkiaSharp rather than QuestPDF's
        // Canvas element: QuestPDF 2025.1.0 throws NotImplementedException for
        // Canvas ("deprecated since 2024.3.0, use .Svg() instead"). Drawing the
        // whole thing with SkiaSharp and placing the result via the stable
        // .Image() API sidesteps that without rewriting the overlay as SVG.
        var pngBytes = RenderReceiptImage(data);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(1000f, 1000f * CanvasHeight / TemplateWidth);
                page.Margin(0);
                page.Content().Image(pngBytes).FitArea();
            });
        });
    }

    private static byte[] RenderReceiptImage(ContributionReceiptData data)
    {
        var amountWhole = (long)Math.Truncate(data.Amount);
        var amountInWords = NumberToWords(amountWhole) + " Only";
        var amountDisplay = $"{amountWhole:N0}/-";

        using var templateStream = typeof(PdfReceiptService).Assembly
            .GetManifestResourceStream("SocietyManagement.Infrastructure.Resources.FestivalReceiptTemplate.png")
            ?? throw new InvalidOperationException("FestivalReceiptTemplate.png embedded resource not found.");
        using var templateBitmap = SKBitmap.Decode(templateStream);

        using var surface = SKSurface.Create(new SKImageInfo((int)TemplateWidth, (int)CanvasHeight));
        var canvas = surface.Canvas;

        var maroon = new SKColor(0xB0, 0x3A, 0x2E);
        var navy = new SKColor(0x1A, 0x3D, 0x8B);
        var paperBackground = new SKColor(0xFF, 0xF8, 0xE6);
        var fullyPaidGreen = new SKColor(0x1E, 0x7D, 0x32);
        var partialOrange = new SKColor(0xC7, 0x6B, 0x00);

        // Fill the whole canvas first so the extra footer strip below the
        // template image (which the bitmap draw below doesn't reach) isn't
        // left transparent/black.
        canvas.Clear(paperBackground);
        canvas.DrawBitmap(templateBitmap, 0, 0);

        // The template ships with a baked-in "Receipt No.:" field near the
        // title (~y=330), a 4th blank header line (~y=208) that nothing draws
        // on, and a generic circular society logo (~x=320-440,y=175-270) that
        // doesn't belong to this society. All erased here: the receipt number
        // is shown once, higher up right after the address, using that 4th
        // line instead — as plain text, so its own underline is erased too.
        EraseRegion(canvas, 1040, 308, 280, 45, paperBackground);
        EraseRegion(canvas, 325, 203, 705, 13, paperBackground);
        EraseRegion(canvas, 315, 172, 130, 100, paperBackground);

        // The template's 3 remaining header lines (~y=83,127,167, under the
        // headline/society/address text) are erased too, per the "remove
        // every underline" request — the text above each still reads fine
        // without them. A 5th, separate divider line (~y=248, between the
        // receipt number and the "Payment Receipt" title) is erased too.
        EraseRegion(canvas, 400, 78, 630, 92, paperBackground);
        EraseRegion(canvas, 330, 240, 700, 14, paperBackground);

        const float fieldSize = 18f;
        const float labelSize = 21f;

        // Top block: opening invocation, society name, address, receipt number.
        // The invocation is a pre-rendered image (Resources/MantraHeader.png),
        // not live-drawn text: SkiaSharp's SKPaint.DrawText has no complex-script
        // shaping, so it renders the "श्री" conjunct as broken/disconnected
        // glyphs. The image was rendered once via WPF/DirectWrite, which shapes
        // Devanagari correctly, and is just scaled/placed here.
        using (var mantraStream = typeof(PdfReceiptService).Assembly
            .GetManifestResourceStream("SocietyManagement.Infrastructure.Resources.MantraHeader.png")
            ?? throw new InvalidOperationException("MantraHeader.png embedded resource not found."))
        using (var mantraBitmap = SKBitmap.Decode(mantraStream))
        {
            const float mantraHeight = 44f;
            var mantraWidth = mantraBitmap.Width * (mantraHeight / mantraBitmap.Height);
            var mantraRect = new SKRect(
                TemplateWidth / 2f - mantraWidth / 2f, 60f,
                TemplateWidth / 2f + mantraWidth / 2f, 60f + mantraHeight);
            canvas.DrawBitmap(mantraBitmap, mantraRect);
        }

        // Shifted down ~22px from the template's original line positions to
        // clear the top border decoration, which the mantra image was sitting
        // too close to.
        DrawCentered(canvas, data.SocietyName, TemplateWidth / 2f, 141, navy, 26, bold: true);
        DrawCentered(canvas, data.SocietyAddress, TemplateWidth / 2f, 181, SKColors.DimGray, 17);
        DrawCentered(canvas, $"Receipt No.: {data.ReceiptNumber}", TemplateWidth / 2f, 224, maroon, 20, bold: true);

        // Every row label below is also baked into the template at a much
        // larger size than the values read comfortably at, so each one is
        // erased and redrawn here at labelSize instead of left as-is.
        // Right-column labels (Date/Flat-Apt/Payment Method/Payment Date) all
        // share one start x so they line up down the page.
        const float rightLabelX = 900f;
        const float rightValueX = 1160f;

        EraseRegion(canvas, 100, 335, 1215, 47, paperBackground);
        DrawLeft(canvas, "Received with thanks from Mr./Ms./Mrs.:", 140, 370, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, "Date:", rightLabelX, 370, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, data.DonorName, 650, 370, SKColors.Black, fieldSize, bold: true);
        DrawLeft(canvas, data.PaymentDate.ToString("dd/MM/yyyy"), rightValueX, 370, SKColors.Black, fieldSize, bold: true);

        EraseRegion(canvas, 100, 377, 1215, 45, paperBackground);
        DrawLeft(canvas, "Collection purpose:", 140, 411, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, data.FestivalName, 420, 411, SKColors.Black, fieldSize, bold: true);

        EraseRegion(canvas, 100, 418, 1215, 46, paperBackground);
        DrawLeft(canvas, "Donor Name:", 140, 453, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, "Flat/Apt. No.:", rightLabelX, 453, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, data.DonorName, 310, 453, SKColors.Black, fieldSize, bold: true);
        if (!string.IsNullOrWhiteSpace(data.FlatNumber))
        {
            DrawLeft(canvas, data.FlatNumber, rightValueX, 453, SKColors.Black, fieldSize, bold: true);
        }

        // Payment Method/Date used to sit on their own row below; they now
        // ride along the right column of the Amount Paid/Amount in Words
        // rows instead, which shortens the receipt by a full row.
        EraseRegion(canvas, 100, 459, 1215, 46, paperBackground);
        DrawLeft(canvas, "Amount Paid (INR):", 140, 494, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, $"₹{amountDisplay}", 400, 494, SKColors.Black, fieldSize, bold: true);
        DrawLeft(canvas, "Payment Method:", rightLabelX, 494, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, data.PaymentMethod, rightValueX, 494, SKColors.Black, fieldSize, bold: true);

        EraseRegion(canvas, 100, 501, 1215, 45, paperBackground);
        DrawLeft(canvas, "Amount in Words:", 140, 536, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, amountInWords, 350, 536, SKColors.Black, fieldSize, bold: true);
        DrawLeft(canvas, "Payment Date:", rightLabelX, 536, SKColors.Black, labelSize, bold: true);
        DrawLeft(canvas, data.PaymentDate.ToString("dd/MM/yyyy"), rightValueX, 536, SKColors.Black, fieldSize, bold: true);

        // The old Payment Method/Date row is now blank (its baked labels and
        // line still need erasing) and Status moves up into that freed slot.
        EraseRegion(canvas, 100, 542, 1215, 46, paperBackground);

        // The template's two decorative amount pills are erased and replaced
        // with Amount Remaining — the amount itself is already shown above
        // in "Amount Paid (INR)", so it isn't repeated here.
        EraseRegion(canvas, 90, 588, 830, 70, paperBackground);

        if (data.TargetAmount.HasValue)
        {
            var remaining = data.TargetAmount.Value - data.TotalPaidForFlat;
            if (remaining > 0m)
            {
                var remainingDisplay = $"{(long)Math.Truncate(remaining):N0}/-";
                DrawLeft(canvas, "Status: Partially Paid", 140, 577, partialOrange, 21, bold: true);
                DrawLeft(canvas, $"Amount Remaining: ₹{remainingDisplay}", 140, 615, partialOrange, 21, bold: true);
            }
            else
            {
                DrawLeft(canvas, "Status: Fully Paid", 140, 577, fullyPaidGreen, 21, bold: true);
            }
        }

        DrawCentered(canvas, "This is a system-generated receipt and does not require a signature.",
            TemplateWidth / 2f, TemplateHeight + 26, SKColors.Gray, 14);

        using var image = surface.Snapshot();
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        return pngData.ToArray();
    }

    public byte[] GenerateFinanceReceipt(FinanceReceiptData data)
    {
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
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(headerColumn =>
                {
                    headerColumn.Item().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text(data.SocietyName).FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                            column.Item().Text(data.SourceLabel + " Receipt").FontSize(13).SemiBold();
                        });
                        //if (logoBytes is not null)
                        //{
                        //    row.ConstantItem(60).Height(60).Image(logoBytes).FitArea();
                        //}
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
                    Row("Paid By", data.PayerName);
                    if (!string.IsNullOrWhiteSpace(data.FlatNumber)) Row("Flat", data.FlatNumber);
                    Row("Description", data.Description);
                    Row("Amount", $"Rs. {data.Amount:N2}");
                    if (!string.IsNullOrWhiteSpace(data.PaymentMethod)) Row("Payment Method", data.PaymentMethod);
                    Row("Payment Date", data.PaymentDate.ToString("dd MMM yyyy"));
                });

                page.Footer().AlignCenter().Text(
                    "This is a system-generated receipt and does not require a signature.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }

    private static void DrawLeft(SKCanvas canvas, string text, float x, float y, SKColor color, float size, bool bold = false, string fontFamily = "Arial")
    {
        using var paint = BuildTextPaint(color, size, bold, SKTextAlign.Left, fontFamily);
        canvas.DrawText(text, x, y, paint);
    }

    private static void DrawCentered(SKCanvas canvas, string text, float centerX, float y, SKColor color, float size, bool bold = false, string fontFamily = "Arial")
    {
        using var paint = BuildTextPaint(color, size, bold, SKTextAlign.Center, fontFamily);
        canvas.DrawText(text, centerX, y, paint);
    }

    /// <summary>Paints over a region of the baked-in template with a flat
    /// fill — used to remove template elements (unused lines, decorative
    /// pills) that the overlay no longer needs, approximating the
    /// surrounding paper color rather than matching its texture exactly.</summary>
    private static void EraseRegion(SKCanvas canvas, float x, float y, float width, float height, SKColor fillColor)
    {
        using var paint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(x, y, width, height, paint);
    }

    private static SKPaint BuildTextPaint(SKColor color, float size, bool bold, SKTextAlign align, string fontFamily = "Arial") => new()
    {
        Color = color,
        IsAntialias = true,
        TextSize = size,
        TextAlign = align,
        Typeface = SKTypeface.FromFamilyName(fontFamily, bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
    };

    private static readonly string[] Ones =
    {
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    };

    private static readonly string[] Tens =
    {
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    };

    /// <summary>Indian numbering (Crore/Lakh/Thousand), matching the receipt
    /// template's "Two Thousand Five Hundred and One Only" style.</summary>
    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(-number);

        var parts = new List<string>();
        var crore = number / 10000000; number %= 10000000;
        var lakh = number / 100000; number %= 100000;
        var thousand = number / 1000; number %= 1000;

        if (crore > 0) parts.Add(ThreeDigitToWords(crore) + " Crore");
        if (lakh > 0) parts.Add(ThreeDigitToWords(lakh) + " Lakh");
        if (thousand > 0) parts.Add(ThreeDigitToWords(thousand) + " Thousand");
        if (number > 0) parts.Add(ThreeDigitToWords(number));

        return string.Join(" ", parts);
    }

    private static string ThreeDigitToWords(long number)
    {
        var words = new List<string>();
        if (number >= 100)
        {
            words.Add(Ones[number / 100] + " Hundred");
            number %= 100;
            if (number > 0) words.Add("and");
        }

        if (number >= 20)
        {
            words.Add(Tens[number / 10]);
            if (number % 10 > 0) words.Add(Ones[number % 10]);
        }
        else if (number > 0)
        {
            words.Add(Ones[number]);
        }

        return string.Join(" ", words);
    }
}
