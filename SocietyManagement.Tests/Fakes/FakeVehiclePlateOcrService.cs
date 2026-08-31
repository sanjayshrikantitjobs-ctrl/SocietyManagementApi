using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Tests.Fakes;

/// <summary>Never runs real OpenCV/Tesseract — returns an empty result by
/// default so handler tests unrelated to OCR aren't coupled to it.</summary>
public class FakeVehiclePlateOcrService : IVehiclePlateOcrService
{
    public PlateOcrResult Result { get; set; } = new(string.Empty, string.Empty, 0);

    public Task<PlateOcrResult> RecognizeAsync(byte[] fullImageBytes, IReadOnlyList<PlatePoint> corners, CancellationToken ct = default)
        => Task.FromResult(Result);
}
