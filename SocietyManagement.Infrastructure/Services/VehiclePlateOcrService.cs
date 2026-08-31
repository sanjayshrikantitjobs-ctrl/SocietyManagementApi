using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SocietyManagement.Application.Common.Helpers;
using SocietyManagement.Application.Common.Interfaces;
using TesseractOCR;
using TesseractOCR.Enums;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>Adapted from a standalone POC that read plates well but hardcoded
/// its 4 perspective-warp source points and its border-crop Rect for one
/// sample photo's exact framing. Those two are NOT the same kind of
/// "hardcoded": the border-crop operates on the warp's fixed 800x200 output
/// canvas, so it already generalizes across photos — only the SOURCE points
/// (where the plate actually sits in a given photo) needed to stop being
/// hardcoded. Here they come from the caller (Vehicle Security's drag-to-crop
/// UI, four independently draggable corners), while the rest of the POC's
/// pipeline — warp, border-crop, upscale, sharpen, Tesseract config — is
/// unchanged.</summary>
public class VehiclePlateOcrService : IVehiclePlateOcrService
{
    private static readonly PlateOcrResult Empty = new(string.Empty, string.Empty, 0);

    /// <summary>The perspective warp's fixed output size — every photo's
    /// marked plate region gets rectified into this same canvas, which is
    /// what makes the border-crop below valid regardless of the source
    /// photo's own resolution or framing.</summary>
    private static readonly Size WarpTargetSize = new(800, 200);

    private readonly string _tessDataPath;
    private readonly ILogger<VehiclePlateOcrService> _logger;

    public VehiclePlateOcrService(ILogger<VehiclePlateOcrService> logger)
    {
        _logger = logger;
        _tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
    }

    public Task<PlateOcrResult> RecognizeAsync(byte[] fullImageBytes, IReadOnlyList<PlatePoint> corners, CancellationToken ct = default)
    {
        if (fullImageBytes.Length == 0 || corners.Count != 4) return Task.FromResult(Empty);

        try
        {
            return Task.FromResult(Recognize(fullImageBytes, corners));
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or BadImageFormatException)
        {
            // Native OpenCV/Tesseract binaries failed to load (missing
            // runtime asset, tessdata missing, bad deploy) — an
            // infrastructure failure, not "bad photo". Logged loudly so a
            // broken deploy doesn't silently degrade to "OCR never
            // prefills anything" with nothing surfaced anywhere.
            _logger.LogError(ex, "Vehicle plate OCR native dependencies failed to load.");
            return Task.FromResult(Empty);
        }
        catch (Exception ex)
        {
            // Unreadable/garbage photo or degenerate points — expected often
            // enough (this is only ever an assist) that it doesn't warrant
            // more than a debug log.
            _logger.LogDebug(ex, "Vehicle plate OCR could not process the marked region.");
            return Task.FromResult(Empty);
        }
    }

    private PlateOcrResult Recognize(byte[] fullImageBytes, IReadOnlyList<PlatePoint> corners)
    {
        using var original = Cv2.ImDecode(fullImageBytes, ImreadModes.Color);
        if (original.Empty()) return Empty;

        using var plate = CorrectPlatePerspective(original, corners);

        using var gray = new Mat();
        Cv2.CvtColor(plate, gray, ColorConversionCodes.BGR2GRAY);

        // Trims the warp's own border/IND-section artifacts — valid for any
        // source photo because it's expressed against WarpTargetSize, not
        // against the original photo's dimensions.
        var roi = new OpenCvSharp.Rect(100, 20, gray.Width - 110, gray.Height - 40);
        using var cropped = new Mat(gray, roi);

        using var resized = new Mat();
        Cv2.Resize(cropped, resized, new Size(cropped.Width * 4, cropped.Height * 4), 0, 0, InterpolationFlags.Lanczos4);

        using var blurred = new Mat();
        Cv2.GaussianBlur(resized, blurred, new Size(0, 0), 3);

        using var sharpened = new Mat();
        Cv2.AddWeighted(resized, 1.6, blurred, -0.6, 0, sharpened);

        Cv2.ImEncode(".png", sharpened, out var encoded);

        var trainedData = Path.Combine(_tessDataPath, "eng.traineddata");
        if (!File.Exists(trainedData))
        {
            _logger.LogError("Vehicle plate OCR trained data not found at {Path}.", trainedData);
            return Empty;
        }

        using var engine = new Engine(_tessDataPath, Language.English, EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

        using var pix = TesseractOCR.Pix.Image.LoadFromMemory(encoded);
        using var page = engine.Process(pix, PageSegMode.SingleLine);

        var rawText = page.Text?.Trim().ToUpperInvariant() ?? string.Empty;
        var confidence = page.MeanConfidence;

        var normalized = VehicleNumberNormalizer.Normalize(rawText);
        if (normalized.Length == 10)
        {
            normalized = ApplyPositionalCorrection(normalized);
        }

        return new PlateOcrResult(rawText, normalized, confidence);
    }

    /// <summary>Straight port of the POC's CorrectPlatePerspective — only the
    /// source points are now a parameter instead of literals.</summary>
    private static Mat CorrectPlatePerspective(Mat image, IReadOnlyList<PlatePoint> corners)
    {
        var source = corners.Select(p => new Point2f((float)p.X, (float)p.Y)).ToArray();
        var destination = new[]
        {
            new Point2f(0, 0),
            new Point2f(WarpTargetSize.Width, 0),
            new Point2f(WarpTargetSize.Width, WarpTargetSize.Height),
            new Point2f(0, WarpTargetSize.Height)
        };

        using var matrix = Cv2.GetPerspectiveTransform(source, destination);
        var result = new Mat();
        Cv2.WarpPerspective(image, result, matrix, WarpTargetSize);
        return result;
    }

    /// <summary>Corrects OCR's most common character confusions at the fixed
    /// positions a standard "SS RR LL NNNN" plate must hold letters vs
    /// digits — only ever applied once the text is already the right
    /// length, so this can't mangle a genuinely different format.</summary>
    private static string ApplyPositionalCorrection(string text)
    {
        var chars = text.ToCharArray();
        chars[2] = ToDigit(chars[2]);
        chars[3] = ToDigit(chars[3]);
        chars[4] = ToLetter(chars[4]);
        chars[5] = ToLetter(chars[5]);
        for (var i = 6; i <= 9; i++)
        {
            chars[i] = ToDigit(chars[i]);
        }
        return new string(chars);
    }

    private static char ToDigit(char c) => c switch
    {
        'O' or 'Q' or 'D' => '0',
        'I' or 'L' => '1',
        'Z' => '2',
        'S' => '5',
        'G' => '6',
        'B' => '8',
        _ => c
    };

    private static char ToLetter(char c) => c switch
    {
        '0' => 'O',
        '1' => 'I',
        '2' => 'Z',
        '5' => 'S',
        '6' => 'G',
        '8' => 'B',
        _ => c
    };
}
