using Microsoft.Extensions.Logging.Abstractions;
using SocietyManagement.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace SocietyManagement.Tests.Vehicles;

public class AsposeOcrVehicleOcrServiceTests
{
    private readonly ITestOutputHelper _output;

    public AsposeOcrVehicleOcrServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string SamplePlatePhotoPath =>
        Path.Combine(AppContext.BaseDirectory, "Vehicles", "Fixtures", "vehicle_sample.jpeg");

    [Fact]
    public async Task RecognizeAsync_CorruptImageBytes_ReturnsFailureInsteadOfThrowing()
    {
        var service = new AsposeOcrVehicleOcrService(NullLogger<AsposeOcrVehicleOcrService>.Instance);

        var result = await service.RecognizeAsync(new byte[] { 1, 2, 3, 4 });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// Real-world regression check using the same actual gate photo used
    /// throughout this session (a Mahindra Alturas G4, plate MH01DK8525). Not
    /// a hard exact-match assert — real-world OCR isn't guaranteed 100%, and
    /// the point of this test is visibility (what does it actually read,
    /// logged every run) plus proving the trial-watermark strip and
    /// format-position correction both ran.
    /// </summary>
    [Fact]
    public async Task RecognizeAsync_RealPlatePhoto_LogsWhatItActuallyReads()
    {
        Assert.True(File.Exists(SamplePlatePhotoPath), $"Fixture not found at {SamplePlatePhotoPath}");
        var imageBytes = await File.ReadAllBytesAsync(SamplePlatePhotoPath);

        var service = new AsposeOcrVehicleOcrService(NullLogger<AsposeOcrVehicleOcrService>.Instance);
        var result = await service.RecognizeAsync(imageBytes);

        _output.WriteLine($"Success={result.Success} Confidence={result.Confidence:F2} RawText=\"{result.RawText}\" ErrorMessage={result.ErrorMessage}");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.RawText);
        Assert.DoesNotContain("Trial License", result.RawText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("MHO1DK852", "MH01DK852")] // the exact case reported live: O misread instead of the RTO code's 0
    [InlineData("MH01DK8525", "MH01DK8525")] // already-correct input is left alone
    [InlineData("DL3CAB1Z34", "DL3CAB1234")] // Z in the trailing number block corrected to 2
    public void CorrectIndianPlateConfusions_FixesPositionalDigitLetterConfusions(string input, string expected)
    {
        Assert.Equal(expected, AsposeOcrVehicleOcrService.CorrectIndianPlateConfusions(input));
    }

    /// <summary>The exact real-world case observed live: Aspose's car-plate
    /// mode read past the plate into a nearby "ALTURAS" model badge
    /// ("ALITUUR"). The correct plate is the first 10 characters — this
    /// should be trimmed out via the format-matching prefix search, not left
    /// contaminated or (worse) corrected as if the trailing text were digits.</summary>
    [Fact]
    public void CorrectIndianPlateConfusions_TrimsTrailingContaminationFromNearbyText()
    {
        Assert.Equal("MH01DK8525", AsposeOcrVehicleOcrService.CorrectIndianPlateConfusions("MH01DK8525ALITUUR"));
    }

    [Fact]
    public void CorrectIndianPlateConfusions_LeavesShortInputUnchanged()
    {
        Assert.Equal("AB12", AsposeOcrVehicleOcrService.CorrectIndianPlateConfusions("AB12"));
    }

    [Theory]
    [InlineData("MH01DK8525\n ************* Trial Licenses ************* .\n", "MH01DK8525")]
    [InlineData("MH01DK8525", "MH01DK8525")]
    [InlineData("\n\n ***** Trial Licenses ***** \n", "")]
    public void StripTrialWatermark_RemovesWatermarkLines(string raw, string expected)
    {
        Assert.Equal(expected, AsposeOcrVehicleOcrService.StripTrialWatermark(raw));
    }
}
