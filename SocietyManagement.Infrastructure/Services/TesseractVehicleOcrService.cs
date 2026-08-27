using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SocietyManagement.Application.Common.Interfaces;
using Tesseract;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Real, open-source OCR for the Vehicle Security scan flow, replacing
/// StubVehicleOcrService. Uses Tesseract (the open-source OCR engine, via the
/// `Tesseract` NuGet wrapper) plus ImageSharp for preprocessing — no cloud API,
/// no per-call cost.
///
/// Tesseract itself only does text RECOGNITION, not plate DETECTION — handed a
/// whole-vehicle photo it has no idea where the plate is and will happily read
/// grille trim or badge lettering instead. <see cref="LocatePlateRegion"/> below
/// closes that gap with a classical (non-ML) computer-vision heuristic: plates
/// are a solid, high-contrast, roughly-rectangular bright region on a vehicle,
/// so an Otsu-thresholded brightness mask + connected-component search finds
/// the most plate-shaped bright blob and crops to it before OCR runs. This is
/// naturally best-effort (no training data, no learned model) so it always
/// falls back to running OCR on the whole image if no plausible region is found
/// — never a hard failure. A full ANPR pipeline (a trained plate-detector model)
/// would be more accurate still; that's real future work the IVehicleOcrService
/// seam already allows for as a drop-in replacement.
///
/// TesseractEngine isn't safe to share across concurrent calls, and constructing
/// one reloads the trained-data file — acceptable for this app's expected
/// gate-scan call volume, so one is created and disposed per recognition rather
/// than pooled.
/// </summary>
public class TesseractVehicleOcrService : IVehicleOcrService
{
    private const int MinPlateWidthPx = 40;
    private const int MinPlateHeightPx = 10;
    private const double MinPlateAspect = 1.8;
    private const double MaxPlateAspect = 7.0;
    // A tight, well-framed gate photo can legitimately have the plate spanning
    // nearly the entire frame width — confirmed live: a real plate crop at 96%
    // of the source width was being wrongly rejected here. Aspect ratio, fill
    // ratio, and the character-blob check below already do the real work of
    // telling a plate apart from a wide bright background; this cap only needs
    // to catch the truly degenerate case of a candidate spanning the *whole*
    // image (e.g. an overexposed sky filling the frame edge-to-edge).
    private const double MaxPlateWidthFraction = 0.98;
    private const double CropPaddingFraction = 0.12; // Otsu often shaves a few px off real edges — pad before OCR
    private const int MinOcrInputHeightPx = 120; // upscale small crops; Tesseract reads short text much better once it's not tiny

    private readonly string _tessDataPath;
    private readonly ILogger<TesseractVehicleOcrService> _logger;

    public TesseractVehicleOcrService(string tessDataPath, ILogger<TesseractVehicleOcrService> logger)
    {
        _tessDataPath = tessDataPath;
        _logger = logger;
    }

    public Task<VehicleOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        try
        {
            using var source = Image.Load<Rgba32>(imageBytes);

            var plateRegion = LocatePlateRegion(source);
            using var ocrInput = plateRegion is { } region
                ? CropAndUpscale(source, region)
                : source.Clone(ctx => ctx.Grayscale());

            if (plateRegion is { } located)
            {
                _logger.LogInformation(
                    "Vehicle OCR: located a plate-shaped region at {X},{Y} {W}x{H} (source {SW}x{SH}); cropping before recognition.",
                    located.X, located.Y, located.Width, located.Height, source.Width, source.Height);
            }
            else
            {
                _logger.LogInformation("Vehicle OCR: no plate-shaped region found; running OCR on the full image.");
            }

            using var ms = new MemoryStream();
            ocrInput.Save(ms, new PngEncoder());
            var preprocessed = ms.ToArray();

            // No tessedit_char_whitelist here deliberately — it's a legacy-engine
            // feature that doesn't reliably work with LSTM-only trained data (which
            // is exactly what the "fast" eng.traineddata is): it silently discards
            // the assembled text after confidence is already computed on it,
            // leaving GetText() empty while Confidence still reports a real number
            // (confirmed live: Confidence 0.95, RawText ""). The same alphanumeric
            // filtering already happens downstream in VehicleNumberNormalizer, so
            // nothing is lost by relying on that instead.
            using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);

            using var pix = Pix.LoadFromMemory(preprocessed);
            // Once cropped tightly to a located plate, the image is essentially a
            // single line of text — SingleLine is far more accurate than a mode
            // built for scattered/sparse text. Without a located region we still
            // don't know the layout, so SparseText (Tesseract's own recommendation
            // for isolated words in a busy image) remains the safer fallback.
            var psm = plateRegion is not null ? PageSegMode.SingleLine : PageSegMode.SparseText;
            using var page = engine.Process(pix, psm);

            var rawText = page.GetText()?.Trim() ?? string.Empty;
            var confidence = page.GetMeanConfidence();

            return Task.FromResult(new VehicleOcrResult(Success: true, RawText: rawText, Confidence: confidence, ErrorMessage: null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract OCR failed to process the scanned image.");
            return Task.FromResult(new VehicleOcrResult(Success: false, RawText: null, Confidence: 0, ErrorMessage: ex.Message));
        }
    }

    // A plate needs at least this many distinct character-shaped dark blobs
    // inside its bright bounding box before it's trusted as "text-bearing" —
    // this is what rejects a plain bright surface (floor tiles, a wall, sky)
    // that happens to share a plate-like size/aspect/fill ratio. Confirmed
    // necessary live: without it, a photo's bright floor tiles at the bottom
    // of the frame outscored the actual plate and were cropped instead.
    private const int MinCharacterBlobs = 3;

    /// <summary>
    /// Finds the most plate-shaped region in the photo: Otsu-threshold the
    /// grayscale image into a bright/dark mask, flood-fill connected
    /// components of "bright" pixels, filter to plate-like size/aspect/fill
    /// ratio, then — critically — require each surviving candidate to
    /// actually contain several character-shaped dark blobs (the plate's own
    /// text) before trusting it, which is what separates a real plate from
    /// any other bright rectangular surface in the photo (floor, wall, sky).
    /// Indian plates are a solid white/yellow rectangle with dark text, so
    /// this works without any trained model. Returns null when nothing
    /// plausible is found (a cluttered photo, an already-tightly-cropped
    /// plate image, etc.) so the caller can fall back to running OCR on the
    /// whole image instead.
    /// </summary>
    internal static Rectangle? LocatePlateRegion(Image<Rgba32> source)
    {
        int width = source.Width;
        int height = source.Height;
        if (width < MinPlateWidthPx || height < MinPlateHeightPx) return null;

        var gray = new byte[width * height];
        var histogram = new int[256];

        source.ProcessPixelRows(accessor =>
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

        int threshold = OtsuThreshold(histogram, width * height);
        var fullArea = new Rectangle(0, 0, width, height);
        var brightComponents = FindConnectedComponents(gray, width, fullArea, v => v > threshold);

        Rectangle? best = null;
        int bestCharacterBlobs = 0;
        long bestArea = 0;

        foreach (var (bounds, pixelCount) in brightComponents)
        {
            if (bounds.Width < MinPlateWidthPx || bounds.Height < MinPlateHeightPx) continue;
            if (bounds.Width > width * MaxPlateWidthFraction) continue;

            double aspect = (double)bounds.Width / bounds.Height;
            if (aspect < MinPlateAspect || aspect > MaxPlateAspect) continue;

            // A plate is a fairly solid rectangle (white background with dark
            // text carved out of it) — require the bright pixels to fill most
            // of their own bounding box, which rules out sparse scattered
            // highlights (chrome trim, reflections) that happen to share a
            // bounding box with a plate-like aspect ratio.
            double fillRatio = (double)pixelCount / (bounds.Width * (double)bounds.Height);
            if (fillRatio < 0.45) continue;

            int characterBlobs = CountCharacterLikeBlobs(gray, width, bounds, threshold);
            if (characterBlobs < MinCharacterBlobs) continue;

            if (characterBlobs > bestCharacterBlobs || (characterBlobs == bestCharacterBlobs && pixelCount > bestArea))
            {
                bestCharacterBlobs = characterBlobs;
                bestArea = pixelCount;
                best = bounds;
            }
        }

        return best;
    }

    /// <summary>Counts dark connected components inside <paramref name="area"/>
    /// that are plausibly a single character's stroke: tall enough relative to
    /// the plate's own height (a character spans most of the plate height) but
    /// narrow relative to its width (rules out a full-width grout line or
    /// shadow band, which a floor/wall surface would produce instead).</summary>
    internal static int CountCharacterLikeBlobs(byte[] gray, int imageWidth, Rectangle area, int threshold)
    {
        var darkComponents = FindConnectedComponents(gray, imageWidth, area, v => v <= threshold);

        int count = 0;
        foreach (var (bounds, pixelCount) in darkComponents)
        {
            if (pixelCount < 6) continue; // noise
            if (bounds.Height < area.Height * 0.25 || bounds.Height > area.Height * 0.95) continue;
            if (bounds.Width > area.Width * 0.5) continue;
            count++;
        }

        return count;
    }

    /// <summary>Generic 4-connectivity flood-fill over pixels within
    /// <paramref name="area"/> satisfying <paramref name="isForeground"/>,
    /// used both for the whole-image "bright" pass and the per-candidate
    /// "dark text inside this bounding box" pass. Indexes are scoped to
    /// <paramref name="area"/>'s own size rather than the full image, so a
    /// small sub-rectangle scan doesn't need a full-image-sized allocation.</summary>
    internal static List<(Rectangle Bounds, long PixelCount)> FindConnectedComponents(
        byte[] gray, int imageWidth, Rectangle area, Func<byte, bool> isForeground)
    {
        int w = area.Width, h = area.Height;
        var visited = new bool[w * h];
        var queue = new int[w * h];
        var result = new List<(Rectangle, long)>();

        for (int ly = 0; ly < h; ly++)
        {
            for (int lx = 0; lx < w; lx++)
            {
                int localIdx = ly * w + lx;
                if (visited[localIdx]) continue;
                if (!isForeground(gray[(area.Y + ly) * imageWidth + (area.X + lx)])) continue;

                int head = 0, tail = 0;
                queue[tail++] = localIdx;
                visited[localIdx] = true;

                int minLx = lx, maxLx = lx, minLy = ly, maxLy = ly;
                long pixelCount = 0;

                while (head < tail)
                {
                    int cur = queue[head++];
                    int clx = cur % w, cly = cur / w;
                    pixelCount++;
                    if (clx < minLx) minLx = clx;
                    if (clx > maxLx) maxLx = clx;
                    if (cly < minLy) minLy = cly;
                    if (cly > maxLy) maxLy = cly;

                    TryEnqueue(clx - 1, cly);
                    TryEnqueue(clx + 1, cly);
                    TryEnqueue(clx, cly - 1);
                    TryEnqueue(clx, cly + 1);

                    void TryEnqueue(int nlx, int nly)
                    {
                        if (nlx < 0 || nlx >= w || nly < 0 || nly >= h) return;
                        int nIdx = nly * w + nlx;
                        if (visited[nIdx]) return;
                        if (!isForeground(gray[(area.Y + nly) * imageWidth + (area.X + nlx)])) return;
                        visited[nIdx] = true;
                        queue[tail++] = nIdx;
                    }
                }

                result.Add((new Rectangle(area.X + minLx, area.Y + minLy, maxLx - minLx + 1, maxLy - minLy + 1), pixelCount));
            }
        }

        return result;
    }

    /// <summary>Standard Otsu's method: picks the brightness threshold that
    /// best separates the histogram into two classes (background vs. bright
    /// foreground) by maximizing between-class variance.</summary>
    internal static int OtsuThreshold(int[] histogram, int totalPixels)
    {
        long sumAll = 0;
        for (int i = 0; i < 256; i++) sumAll += (long)i * histogram[i];

        long sumBackground = 0;
        int weightBackground = 0;
        double maxVariance = 0;
        int threshold = 128;

        for (int t = 0; t < 256; t++)
        {
            weightBackground += histogram[t];
            if (weightBackground == 0) continue;

            int weightForeground = totalPixels - weightBackground;
            if (weightForeground == 0) break;

            sumBackground += (long)t * histogram[t];

            double meanBackground = (double)sumBackground / weightBackground;
            double meanForeground = (double)(sumAll - sumBackground) / weightForeground;
            double betweenVariance = (double)weightBackground * weightForeground *
                (meanBackground - meanForeground) * (meanBackground - meanForeground);

            if (betweenVariance > maxVariance)
            {
                maxVariance = betweenVariance;
                threshold = t;
            }
        }

        return threshold;
    }

    /// <summary>Crops to the located region (with padding, clamped to image
    /// bounds), converts to grayscale, and upscales small crops — plate text
    /// in a phone photo is often only a few dozen pixels tall, and Tesseract's
    /// accuracy on short text improves substantially once it isn't tiny.</summary>
    private static Image<Rgba32> CropAndUpscale(Image<Rgba32> source, Rectangle region)
    {
        int padX = (int)(region.Width * CropPaddingFraction);
        int padY = (int)(region.Height * CropPaddingFraction);

        int x = Math.Max(0, region.X - padX);
        int y = Math.Max(0, region.Y - padY);
        int right = Math.Min(source.Width, region.X + region.Width + padX);
        int bottom = Math.Min(source.Height, region.Y + region.Height + padY);

        var padded = new Rectangle(x, y, right - x, bottom - y);
        var crop = source.Clone(ctx => ctx.Crop(padded));

        if (crop.Height < MinOcrInputHeightPx)
        {
            double scale = (double)MinOcrInputHeightPx / crop.Height;
            crop.Mutate(ctx => ctx.Resize((int)(crop.Width * scale), MinOcrInputHeightPx));
        }

        crop.Mutate(ctx => ctx.Grayscale());
        return crop;
    }
}
