using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SocietyManagement.Infrastructure.Services;
using Tesseract;
using Xunit;
using Xunit.Abstractions;

namespace SocietyManagement.Tests.Vehicles;

/// <summary>
/// Exercises the real native pipeline (ImageSharp preprocessing -> Tesseract
/// engine -> eng.traineddata -> confidence extraction) end-to-end — the thing
/// most likely to break for a newly-wired native dependency isn't OCR accuracy,
/// it's the native binaries or trained-data file failing to load at all. A
/// plain white image can't prove *accuracy* (there's no real plate photo to
/// assert against in an automated test), but a clean run proves the whole
/// pipeline is wired correctly, which is what these tests are for.
/// </summary>
public class TesseractVehicleOcrServiceTests
{
    private readonly ITestOutputHelper _output;

    public TesseractVehicleOcrServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string TessDataPath => Path.Combine(AppContext.BaseDirectory, "tessdata");
    private static string SamplePlatePhotoPath => Path.Combine(AppContext.BaseDirectory, "Vehicles", "Fixtures", "vehicle_sample.jpeg");

    private static byte[] BlankPng()
    {
        using var image = new Image<Rgba32>(200, 80, Color.White);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    [Fact]
    public async Task RecognizeAsync_RunsTheFullNativePipeline_WithoutThrowing()
    {
        var service = new TesseractVehicleOcrService(TessDataPath, NullLogger<TesseractVehicleOcrService>.Instance);

        var result = await service.RecognizeAsync(BlankPng());

        // Success=true here means: ImageSharp decoded/preprocessed the image,
        // the native Tesseract/Leptonica binaries loaded, eng.traineddata was
        // read successfully, and a (possibly empty, for a blank image)
        // confidence score came back — i.e. the pipeline itself works.
        Assert.True(result.Success, result.ErrorMessage);
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    [Fact]
    public async Task RecognizeAsync_CorruptImageBytes_ReturnsFailureInsteadOfThrowing()
    {
        var service = new TesseractVehicleOcrService(TessDataPath, NullLogger<TesseractVehicleOcrService>.Instance);

        var result = await service.RecognizeAsync(new byte[] { 1, 2, 3, 4 });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// Simulates the real problem this session hit: a "vehicle photo" is mostly
    /// dark background with a small bright, plate-shaped rectangle somewhere in
    /// it (not centered, not filling the frame) — exactly the shape that made
    /// Tesseract alone (no detection step) read garbage from the wrong region.
    /// Asserts LocatePlateRegion actually finds that rectangle's bounds, not
    /// just that it runs without throwing.
    /// </summary>
    [Fact]
    public void LocatePlateRegion_FindsABrightPlateShapedRectangle_ContainingCharacterLikeBlobs()
    {
        const int imageWidth = 600, imageHeight = 400;
        var plateBounds = new Rectangle(220, 250, 160, 40); // 4:1 aspect, plate-like, off-center low in frame

        using var image = new Image<Rgba32>(imageWidth, imageHeight, Color.DimGray);
        FillRectangle(image, plateBounds, Color.White);
        DrawCharacterBlobs(image, plateBounds, count: 6);

        var located = TesseractVehicleOcrService.LocatePlateRegion(image);

        Assert.NotNull(located);
        // Otsu + component bounds should land close to the drawn rectangle,
        // not necessarily pixel-exact.
        Assert.InRange(located!.Value.X, plateBounds.X - 3, plateBounds.X + 3);
        Assert.InRange(located.Value.Y, plateBounds.Y - 3, plateBounds.Y + 3);
        Assert.InRange(located.Value.Width, plateBounds.Width - 6, plateBounds.Width + 6);
        Assert.InRange(located.Value.Height, plateBounds.Height - 6, plateBounds.Height + 6);
    }

    [Fact]
    public void LocatePlateRegion_ReturnsNull_WhenNothingPlateShapedIsPresent()
    {
        using var image = new Image<Rgba32>(400, 300, Color.DimGray);
        // A bright square (1:1) isn't plate-shaped — should be rejected by the aspect-ratio filter.
        var square = new Rectangle(150, 100, 60, 60);
        FillRectangle(image, square, Color.White);
        DrawCharacterBlobs(image, square, count: 3);

        var located = TesseractVehicleOcrService.LocatePlateRegion(image);

        Assert.Null(located);
    }

    /// <summary>
    /// A plain bright rectangle with no internal texture (e.g. a floor, a wall)
    /// must NOT be picked, even though its size/aspect/fill ratio look
    /// plate-like — this is the exact failure confirmed live on a real gate
    /// photo, where bright floor tiles at the bottom of the frame outscored
    /// the actual plate. Character-blob content is what tells them apart.
    /// </summary>
    [Fact]
    public void LocatePlateRegion_ReturnsNull_ForATexturelessBrightSurface_EvenIfPlateShaped()
    {
        const int imageWidth = 600, imageHeight = 400;
        using var image = new Image<Rgba32>(imageWidth, imageHeight, Color.DimGray);
        FillRectangle(image, new Rectangle(220, 250, 160, 40), Color.White); // no character blobs drawn

        var located = TesseractVehicleOcrService.LocatePlateRegion(image);

        Assert.Null(located);
    }

    /// <summary>
    /// Real-world regression check using an actual gate photo (a Mahindra
    /// Alturas G4, plate MH01DK8525 — clearly legible to a human, taken
    /// head-on with good lighting: about as favorable a case as OCR gets),
    /// not a synthetic image. Doesn't assert an exact match — the point of
    /// this test is to print the real RawText/Confidence/located-region every
    /// CI run so a regression (or a genuine fix) shows up in the log without
    /// needing another live device test to find out.
    /// </summary>
    [Fact]
    public async Task RecognizeAsync_RealPlatePhoto_LogsWhatItActuallyReads()
    {
        Assert.True(File.Exists(SamplePlatePhotoPath), $"Fixture not found at {SamplePlatePhotoPath}");

        var service = new TesseractVehicleOcrService(TessDataPath, NullLogger<TesseractVehicleOcrService>.Instance);
        var imageBytes = await File.ReadAllBytesAsync(SamplePlatePhotoPath);

        using (var image = Image.Load<Rgba32>(imageBytes))
        {
            var located = TesseractVehicleOcrService.LocatePlateRegion(image);
            _output.WriteLine(located is { } r
                ? $"LocatePlateRegion found: X={r.X} Y={r.Y} W={r.Width} H={r.Height} (source {image.Width}x{image.Height})"
                : $"LocatePlateRegion found: nothing (source {image.Width}x{image.Height})");
        }

        var result = await service.RecognizeAsync(imageBytes);

        _output.WriteLine($"Success={result.Success} Confidence={result.Confidence:F2} RawText=\"{result.RawText}\" ErrorMessage={result.ErrorMessage}");

        Assert.True(result.Success, result.ErrorMessage);
    }

    /// <summary>Draws <paramref name="count"/> evenly-spaced dark rectangles
    /// inside <paramref name="area"/>, sized to satisfy CountCharacterLikeBlobs'
    /// thresholds (tall relative to the area, narrow relative to its width) —
    /// simulates a plate's individual characters for the detector's
    /// text-content check.</summary>
    private static void DrawCharacterBlobs(Image<Rgba32> image, Rectangle area, int count)
    {
        int charHeight = (int)(area.Height * 0.7);
        int charWidth = Math.Max(2, area.Width / (count * 2));
        int spacing = area.Width / count;
        int top = area.Y + (area.Height - charHeight) / 2;

        for (int i = 0; i < count; i++)
        {
            int left = area.X + i * spacing + spacing / 4;
            var charBounds = new Rectangle(left, top, charWidth, charHeight);
            FillRectangle(image, charBounds, Color.Black);
        }
    }

    /// <summary>Diagnostic-only: dumps every bright candidate LocatePlateRegion
    /// considered for the real fixture photo, and why each was accepted or
    /// rejected, so a real detection miss can be root-caused from the log
    /// instead of guessing. Not a pass/fail assertion — its value is the
    /// printed output.</summary>
    [Fact]
    public void DiagnoseRealPlatePhoto_LogsEveryCandidateAndWhyItWasRejected()
    {
        var imageBytes = File.ReadAllBytes(SamplePlatePhotoPath);
        using var image = Image.Load<Rgba32>(imageBytes);
        int width = image.Width, height = image.Height;

        var gray = new byte[width * height];
        var histogram = new int[256];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                int rowOffset = y * width;
                for (int x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    byte luminance = (byte)((p.R * 299 + p.G * 587 + p.B * 114) / 1000);
                    gray[rowOffset + x] = luminance;
                    histogram[luminance]++;
                }
            }
        });

        int threshold = TesseractVehicleOcrService.OtsuThreshold(histogram, width * height);
        _output.WriteLine($"Image {width}x{height}, Otsu threshold={threshold}");

        var fullArea = new Rectangle(0, 0, width, height);
        var brightComponents = TesseractVehicleOcrService.FindConnectedComponents(gray, width, fullArea, v => v > threshold);
        _output.WriteLine($"Bright components found: {brightComponents.Count}");

        const int minPlateWidthPx = 40, minPlateHeightPx = 10;
        const double minPlateAspect = 1.8, maxPlateAspect = 7.0, maxPlateWidthFraction = 0.92;

        foreach (var (bounds, pixelCount) in brightComponents.OrderByDescending(c => c.PixelCount).Take(15))
        {
            double aspect = (double)bounds.Width / bounds.Height;
            double fillRatio = pixelCount / (bounds.Width * (double)bounds.Height);

            if (bounds.Width < minPlateWidthPx || bounds.Height < minPlateHeightPx)
            {
                _output.WriteLine($"[{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}] px={pixelCount} -> REJECT: too small");
                continue;
            }
            if (bounds.Width > width * maxPlateWidthFraction)
            {
                _output.WriteLine($"[{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}] px={pixelCount} -> REJECT: spans whole width");
                continue;
            }
            if (aspect < minPlateAspect || aspect > maxPlateAspect)
            {
                _output.WriteLine($"[{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}] px={pixelCount} aspect={aspect:F2} -> REJECT: bad aspect");
                continue;
            }
            if (fillRatio < 0.45)
            {
                _output.WriteLine($"[{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}] px={pixelCount} aspect={aspect:F2} fill={fillRatio:F2} -> REJECT: low fill ratio");
                continue;
            }

            int charBlobs = TesseractVehicleOcrService.CountCharacterLikeBlobs(gray, width, bounds, threshold);
            _output.WriteLine($"[{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}] px={pixelCount} aspect={aspect:F2} fill={fillRatio:F2} charBlobs={charBlobs} -> {(charBlobs >= 3 ? "ACCEPT" : "REJECT: too few character blobs")}");
        }
    }

    /// <summary>Diagnostic-only: saves crops of specific candidate regions from
    /// the real fixture photo to disk for visual inspection, to map detected
    /// bright-component bounding boxes back to what they actually are in the
    /// photo (plate vs. logo vs. chrome trim vs. floor).</summary>
    [Fact]
    public void SaveCandidateCropsForVisualInspection()
    {
        var outDir = Environment.GetEnvironmentVariable("OCR_DEBUG_OUTDIR");
        if (string.IsNullOrWhiteSpace(outDir)) return;
        Directory.CreateDirectory(outDir);

        var imageBytes = File.ReadAllBytes(SamplePlatePhotoPath);
        using var image = Image.Load<Rgba32>(imageBytes);

        var candidates = new (string Name, Rectangle Bounds)[]
        {
            ("merged_widespan", new Rectangle(0, 521, 860, 308)),
            ("logo_guess", new Rectangle(303, 257, 307, 168)),
            ("chrome_trim_guess", new Rectangle(0, 445, 900, 109)),
            ("altura_text_guess", new Rectangle(0, 842, 264, 70)),
            ("wide_plate_guess", new Rectangle(0, 470, 900, 260)),
        };

        foreach (var (name, bounds) in candidates)
        {
            var clamped = Rectangle.Intersect(bounds, new Rectangle(0, 0, image.Width, image.Height));
            using var crop = image.Clone(ctx => ctx.Crop(clamped));
            crop.Save(Path.Combine(outDir, $"{name}.png"), new PngEncoder());
        }

        _output.WriteLine($"Saved {candidates.Length} crops to {outDir}");
    }

    /// <summary>Diagnostic-only: tries several crop-preprocessing variants
    /// against the real, correctly-located plate crop, so the right
    /// preprocessing can be picked from actual OCR output instead of guessing.</summary>
    [Fact]
    public void DiagnosePreprocessingVariants_ForTheRealPlateCrop()
    {
        var imageBytes = File.ReadAllBytes(SamplePlatePhotoPath);
        using var source = Image.Load<Rgba32>(imageBytes);

        var plateRegion = TesseractVehicleOcrService.LocatePlateRegion(source);
        Assert.NotNull(plateRegion);
        var region = plateRegion!.Value;

        int padX = (int)(region.Width * 0.12), padY = (int)(region.Height * 0.12);
        var padded = Rectangle.Intersect(
            new Rectangle(region.X - padX, region.Y - padY, region.Width + 2 * padX, region.Height + 2 * padY),
            new Rectangle(0, 0, source.Width, source.Height));

        using var baseCrop = source.Clone(ctx => ctx.Crop(padded));

        // The plate's left ~20% is the "IND" strip + QR watermark, not the
        // registration number — a real, non-text region that could throw off
        // recognition of the actual alphanumeric text to its right.
        var trimmedBounds = new Rectangle((int)(baseCrop.Width * 0.20), 0, (int)(baseCrop.Width * 0.80), baseCrop.Height);
        using var trimmedCrop = baseCrop.Clone(ctx => ctx.Crop(trimmedBounds));

        var debugOutDir = Environment.GetEnvironmentVariable("OCR_DEBUG_OUTDIR");
        if (!string.IsNullOrWhiteSpace(debugOutDir))
        {
            Directory.CreateDirectory(debugOutDir);
            baseCrop.Save(Path.Combine(debugOutDir, "base_crop.png"), new PngEncoder());
            trimmedCrop.Save(Path.Combine(debugOutDir, "trimmed_crop.png"), new PngEncoder());
        }

        var variants = new (string Name, Func<Image<Rgba32>> Build)[]
        {
            ("grayscale_only", () => { var img = baseCrop.Clone(); img.Mutate(c => c.Grayscale()); return img; }),
            ("grayscale_contrast", () => { var img = baseCrop.Clone(); img.Mutate(c => c.Grayscale().Contrast(1.5f)); return img; }),
            ("grayscale_binarize", () => { var img = baseCrop.Clone(); img.Mutate(c => c.Grayscale().BinaryThreshold(0.6f)); return img; }),
            ("grayscale_contrast_sharpen", () => { var img = baseCrop.Clone(); img.Mutate(c => c.Grayscale().Contrast(1.3f).GaussianSharpen(1.0f)); return img; }),
            ("trimmed_grayscale", () => { var img = trimmedCrop.Clone(); img.Mutate(c => c.Grayscale()); return img; }),
            ("trimmed_grayscale_contrast", () => { var img = trimmedCrop.Clone(); img.Mutate(c => c.Grayscale().Contrast(1.5f)); return img; }),
            ("trimmed_grayscale_binarize", () => { var img = trimmedCrop.Clone(); img.Mutate(c => c.Grayscale().BinaryThreshold(0.6f)); return img; }),
        };

        var upscaleHeights = new[] { 120, 250, 400 };
        var psmModes = new[] { PageSegMode.SingleLine, PageSegMode.SingleWord, PageSegMode.RawLine };

        var tessDataVariants = new (string Label, string Path)[] { ("fast", TessDataPath) };
        var bestTessDataPath = Environment.GetEnvironmentVariable("OCR_DEBUG_TESSDATA_BEST");
        if (!string.IsNullOrWhiteSpace(bestTessDataPath) && Directory.Exists(bestTessDataPath))
        {
            tessDataVariants = tessDataVariants.Append(("best", bestTessDataPath)).ToArray();
        }

        foreach (var (tessLabel, tessPath) in tessDataVariants)
        {
            foreach (var (name, build) in variants)
            {
                foreach (var targetHeight in upscaleHeights)
                {
                    using var variant = build();
                    double scale = (double)targetHeight / variant.Height;
                    variant.Mutate(c => c.Resize((int)(variant.Width * scale), targetHeight));

                    using var ms = new MemoryStream();
                    variant.Save(ms, new PngEncoder());
                    var bytes = ms.ToArray();

                    foreach (var psm in psmModes)
                    {
                        using var engine = new TesseractEngine(tessPath, "eng", EngineMode.Default);
                        using var pix = Pix.LoadFromMemory(bytes);
                        using var page = engine.Process(pix, psm);
                        var text = page.GetText()?.Trim().Replace("\n", "\\n") ?? string.Empty;
                        var confidence = page.GetMeanConfidence();
                        _output.WriteLine($"tessdata={tessLabel} {name} h={targetHeight} psm={psm} -> conf={confidence:F2} text=\"{text}\"");
                    }
                }
            }
        }
    }

    private static void FillRectangle(Image<Rgba32> image, Rectangle bounds, Color color)
    {
        var pixel = color.ToPixel<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
                {
                    row[x] = pixel;
                }
            }
        });
    }
}
