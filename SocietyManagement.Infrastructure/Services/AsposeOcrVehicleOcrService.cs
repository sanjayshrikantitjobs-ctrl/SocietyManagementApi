using System.Text.RegularExpressions;
using Aspose.OCR;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Helpers;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Real OCR for the Vehicle Security scan flow, replacing every earlier
/// attempt this session (Tesseract — reads the wrong region and/or misreads
/// this bold plate font even when correctly cropped; a from-scratch PaddleOCR
/// pipeline — worked, but its native libraries alone added ~540MB to the
/// Linux deployment package and broke the Azure App Service deploy; Azure AI
/// Vision — works but was ruled out to avoid a cloud dependency). Aspose.OCR
/// ships a purpose-built <see cref="AsposeOcr.RecognizeCarPlate"/> mode and,
/// confirmed live against the real gate photo used throughout this session,
/// reads it far more accurately than anything tried before.
///
/// Aspose.OCR is a COMMERCIAL product — confirmed via aspose.com pricing
/// (perpetual licenses from $799, metered from $1,999/month). Unlicensed use
/// appends a "Trial Licenses" watermark line to every recognition result;
/// <see cref="StripTrialWatermark"/> removes it so RawText stays clean
/// regardless of licensing state. Once a paid license is purchased, drop the
/// .lic file path into Aspose:LicenseFilePath (see DependencyInjection.cs) —
/// no code change needed, same "swap in once configured" pattern used
/// elsewhere in this app (Blob Storage, WhatsApp).
///
/// Its only heavy dependency is Microsoft.ML.OnnxRuntime — the same
/// lightweight, cross-platform runtime Azure/most modern OCR tooling uses,
/// nothing like Paddle's own multi-hundred-MB native inference engine — so
/// this doesn't reintroduce the deployment-size problem PaddleOCR caused.
/// </summary>
public class AsposeOcrVehicleOcrService : IVehicleOcrService
{
    // Indian plate format: 2-letter state code, 1-2 digit RTO code, 0-3 letter
    // series, 4-digit number (e.g. MH01DK8525, DL3CAB1234, KA05AB1234).
    private static readonly Regex IndianPlatePattern = new("^[A-Z]{2}[0-9]{1,2}[A-Z]{0,3}[0-9]{4}$", RegexOptions.Compiled);

    // OCR commonly confuses these digit/letter pairs by shape — corrected
    // positionally below, since a plate's format tells you which each
    // position must be, regardless of what glyph the model actually read.
    private static readonly Dictionary<char, char> DigitToLetter = new() { ['0'] = 'O', ['1'] = 'I', ['5'] = 'S', ['8'] = 'B', ['2'] = 'Z', ['6'] = 'G' };
    private static readonly Dictionary<char, char> LetterToDigit = new() { ['O'] = '0', ['I'] = '1', ['L'] = '1', ['S'] = '5', ['B'] = '8', ['Z'] = '2', ['G'] = '6', ['Q'] = '0' };

    private readonly ILogger<AsposeOcrVehicleOcrService> _logger;

    public AsposeOcrVehicleOcrService(ILogger<AsposeOcrVehicleOcrService> logger)
    {
        _logger = logger;
    }

    public Task<VehicleOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        try
        {
            using var input = new OcrInput(InputType.SingleImage);
            using var stream = new MemoryStream(imageBytes);
            input.Add(stream);

            var engine = new AsposeOcr();
            var output = engine.RecognizeCarPlate(input);
            var result = output.FirstOrDefault();

            var cleaned = StripTrialWatermark(result?.RecognitionText ?? string.Empty);
            var alnum = VehicleNumberNormalizer.Normalize(cleaned);
            var corrected = CorrectIndianPlateConfusions(alnum);

            // Not using Aspose's own per-line Confidence: its XML docs state
            // that value "is always set to 0 when using a temporary license" —
            // useless under the current trial build (see class remarks above).
            // Whether the corrected text matches the expected plate format is
            // a real, honest signal instead, and drives the same low-confidence
            // UI badge; revisit once a paid license makes the real score usable.
            var confidence = IndianPlatePattern.IsMatch(corrected) ? 0.9 : (corrected.Length > 0 ? 0.5 : 0.0);

            _logger.LogInformation(
                "Vehicle OCR: Aspose read \"{Raw}\" -> corrected \"{Corrected}\" (confidence {Confidence}).",
                cleaned, corrected, confidence);

            return Task.FromResult(new VehicleOcrResult(Success: true, RawText: corrected, Confidence: confidence, ErrorMessage: null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vehicle OCR: Aspose recognition failed.");
            return Task.FromResult(new VehicleOcrResult(Success: false, RawText: null, Confidence: 0, ErrorMessage: ex.Message));
        }
    }

    /// <summary>Unlicensed Aspose.OCR appends a line like
    /// " ************* Trial Licenses ************* ." to the recognized
    /// text — strips any line containing that marker (and blank lines),
    /// joining what's left. A licensed build simply has nothing to strip.</summary>
    internal static string StripTrialWatermark(string raw)
    {
        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0 && !line.Contains("Trial License", StringComparison.OrdinalIgnoreCase));
        return string.Join(" ", lines).Trim();
    }

    /// <summary>Corrects shape-confusable characters using the Indian plate
    /// format's fixed positions, then trims trailing contamination: Aspose's
    /// car-plate mode sometimes reads past the plate into adjacent text (a
    /// model badge, dealer sticker) — confirmed live, where a read of
    /// "MH01DK8525ALITUUR" had the exact correct plate as its first 10
    /// characters, with "ALITUUR" picked up from a nearby "ALTURAS" badge.
    /// Tries successively shorter prefixes of the corrected read (11 down to
    /// 8 characters, the real range of Indian plate lengths) and returns the
    /// first one that fully matches the plate format; falls back to
    /// correcting the whole string if no prefix matches exactly.</summary>
    internal static string CorrectIndianPlateConfusions(string alnum)
    {
        if (alnum.Length < 7) return alnum; // too short to safely infer fixed-format positions

        for (var length = Math.Min(11, alnum.Length); length >= 8; length--)
        {
            var candidate = CorrectFixedPositions(alnum[..length]);
            if (IndianPlatePattern.IsMatch(candidate)) return candidate;
        }

        return CorrectFixedPositions(alnum);
    }

    /// <summary>The first 2 characters must be letters (state code), the last
    /// 4 must be digits (vehicle number), and — the common 2-digit-RTO-code
    /// case — the 2 characters right after the state code must be digits too.
    /// The middle series segment (variable length, 0-3 letters) is left as
    /// read rather than guessed, since its length isn't knowable from the
    /// total length alone.</summary>
    private static string CorrectFixedPositions(string alnum)
    {
        if (alnum.Length < 7) return alnum;

        var chars = alnum.ToCharArray();

        ApplyCorrection(chars, 0, 2, DigitToLetter);

        var rtoCodeEnd = Math.Min(4, chars.Length - 4);
        if (rtoCodeEnd > 2) ApplyCorrection(chars, 2, rtoCodeEnd, LetterToDigit);

        ApplyCorrection(chars, chars.Length - 4, chars.Length, LetterToDigit);

        return new string(chars);
    }

    private static void ApplyCorrection(char[] chars, int start, int end, IReadOnlyDictionary<char, char> map)
    {
        for (var i = start; i < end; i++)
        {
            if (map.TryGetValue(chars[i], out var replacement)) chars[i] = replacement;
        }
    }
}
