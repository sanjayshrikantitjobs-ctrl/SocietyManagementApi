using SocietyManagement.Infrastructure.Services;
using Xunit;

namespace SocietyManagement.Tests.Vehicles;

/// <summary>
/// Both AzureVisionOcrService and PaddleOcrVehicleOcrService return every text
/// line they find in a vehicle photo — plate, brand badge, dealer sticker,
/// reflections misread as text. PickBestPlateLine (shared between them) is
/// what turns that list into a single plate guess; these tests are the actual
/// behavior contract for that scoring, not just "doesn't throw" coverage.
/// </summary>
public class PlateTextLineScorerTests
{
    [Fact]
    public void PickBestPlateLine_PrefersExactIndianPlateFormat_OverHigherConfidenceNonPlateText()
    {
        var lines = new List<(string Text, double Confidence)>
        {
            ("TOYOTA", 0.99),        // brand badge — high confidence, not plate-shaped
            ("MH01DK8525", 0.62),    // the actual plate — lower confidence, but matches the format
        };

        var best = PlateTextLineScorer.PickBestPlateLine(lines);

        Assert.NotNull(best);
        Assert.Equal("MH01DK8525", best!.Value.Text);
    }

    [Fact]
    public void PickBestPlateLine_FallsBackToHighestConfidence_WhenNoLineMatchesTheExactFormat()
    {
        var lines = new List<(string Text, double Confidence)>
        {
            ("ALTURAS G4", 0.95),    // model badge — fails the digit-count check, excluded outright
            ("MH1DK852", 0.40),      // plate-like (letters+digits, plausible length) but not exact format
            ("KA", 0.90),            // too short to be a plate
        };

        var best = PlateTextLineScorer.PickBestPlateLine(lines);

        Assert.NotNull(best);
        Assert.Equal("MH1DK852", best!.Value.Text);
    }

    [Fact]
    public void PickBestPlateLine_ReturnsNull_WhenNothingIsPlateShaped()
    {
        var lines = new List<(string Text, double Confidence)>
        {
            ("TOYOTA", 0.99),
            ("INDIA", 0.80),
            ("4", 0.50),
        };

        var best = PlateTextLineScorer.PickBestPlateLine(lines);

        Assert.Null(best);
    }

    [Fact]
    public void PickBestPlateLine_ReturnsNull_ForEmptyInput()
    {
        Assert.Null(PlateTextLineScorer.PickBestPlateLine(new List<(string, double)>()));
    }

    [Fact]
    public void PickBestPlateLine_NormalizesToUppercaseAlphanumeric()
    {
        var lines = new List<(string Text, double Confidence)> { ("mh-01 dk 8525", 0.7) };

        var best = PlateTextLineScorer.PickBestPlateLine(lines);

        Assert.NotNull(best);
        Assert.Equal("MH01DK8525", best!.Value.Text);
    }
}
