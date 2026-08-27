using Microsoft.Extensions.Logging.Abstractions;
using SocietyManagement.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace SocietyManagement.Tests.Vehicles;

/// <summary>
/// Constructing PaddleOcrAll loads model weights into native memory — expensive
/// enough that, unlike TesseractVehicleOcrServiceTests (which intentionally
/// constructs a fresh engine per [Fact] because Tesseract is cheap), this class
/// shares one engine across all its tests via IClassFixture.
/// </summary>
public class PaddleOcrEngineFixture : IDisposable
{
    public PaddleOcrVehicleOcrService Service { get; } = new(NullLogger<PaddleOcrVehicleOcrService>.Instance);

    public void Dispose() => Service.Dispose();
}

public class PaddleOcrVehicleOcrServiceTests : IClassFixture<PaddleOcrEngineFixture>
{
    private readonly PaddleOcrVehicleOcrService _service;
    private readonly ITestOutputHelper _output;

    public PaddleOcrVehicleOcrServiceTests(PaddleOcrEngineFixture fixture, ITestOutputHelper output)
    {
        _service = fixture.Service;
        _output = output;
    }

    private static string SamplePlatePhotoPath =>
        Path.Combine(AppContext.BaseDirectory, "Vehicles", "Fixtures", "vehicle_sample.jpeg");

    [Fact]
    public async Task RecognizeAsync_CorruptImageBytes_ReturnsFailureInsteadOfThrowing()
    {
        var result = await _service.RecognizeAsync(new byte[] { 1, 2, 3, 4 });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// Real-world regression check using the same actual gate photo Tesseract
    /// was tested against (a Mahindra Alturas G4, plate MH01DK8525). Not a hard
    /// exact-match assert — real-world OCR isn't guaranteed 100%, and the point
    /// of this test is visibility (what does it actually read, logged every
    /// run) rather than a brittle pass/fail on exact text. Confirms the whole
    /// pipeline (image decode, native model load, detection+recognition) works
    /// end-to-end against a real photo, not just a synthetic one.
    /// </summary>
    [Fact]
    public async Task RecognizeAsync_RealPlatePhoto_LogsWhatItActuallyReads()
    {
        Assert.True(File.Exists(SamplePlatePhotoPath), $"Fixture not found at {SamplePlatePhotoPath}");
        var imageBytes = await File.ReadAllBytesAsync(SamplePlatePhotoPath);

        var result = await _service.RecognizeAsync(imageBytes);

        _output.WriteLine($"Success={result.Success} Confidence={result.Confidence:F2} RawText=\"{result.RawText}\" ErrorMessage={result.ErrorMessage}");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.RawText);
    }
}
