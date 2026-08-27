using System.Text.RegularExpressions;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Shared by every OCR provider that returns multiple candidate text lines from
/// a whole vehicle photo (AzureVisionOcrService, PaddleOcrVehicleOcrService) —
/// a photo yields the plate plus brand badges, model names, dealer/parking
/// stickers as separate recognized lines, and this picks the single most
/// plate-shaped one so nothing downstream (VehicleNumberNormalizer, the
/// confirm/edit UI, DB matching) needs to know multiple candidates existed.
/// </summary>
internal static class PlateTextLineScorer
{
    // Indian plate format: 2-letter state code, 1-2 digit RTO code, 0-3 letter
    // series, 4-digit number (e.g. MH01DK8525, DL3CAB1234, KA05AB1234).
    private static readonly Regex IndianPlatePattern = new("^[A-Z]{2}[0-9]{1,2}[A-Z]{0,3}[0-9]{4}$", RegexOptions.Compiled);

    /// <summary>Scores each detected line by how plate-shaped its alphanumeric
    /// content is: an exact Indian-plate-pattern match wins outright; otherwise
    /// prefers a plausible letter+digit mix of roughly plate length, weighted by
    /// the line's own OCR confidence. Returns null if nothing in the photo looks
    /// remotely plate-like (e.g. only brand/badge text was detected).</summary>
    internal static (string Text, double Confidence)? PickBestPlateLine(IReadOnlyList<(string Text, double Confidence)> lines)
    {
        (string Text, double Confidence, double Score)? best = null;

        foreach (var (text, confidence) in lines)
        {
            var alnum = new string(text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            if (alnum.Length is < 5 or > 11) continue;

            int digitCount = alnum.Count(char.IsDigit);
            int letterCount = alnum.Length - digitCount;
            if (digitCount < 2 || letterCount < 2) continue; // rules out pure-numeric or pure-alpha badge/brand text

            double score = confidence;
            if (IndianPlatePattern.IsMatch(alnum)) score += 10; // exact-format match dominates any confidence difference

            if (best is null || score > best.Value.Score)
            {
                best = (alnum, confidence, score);
            }
        }

        return best is { } b ? (b.Text, b.Confidence) : null;
    }
}
