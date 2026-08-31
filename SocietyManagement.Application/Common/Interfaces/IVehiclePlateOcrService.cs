namespace SocietyManagement.Application.Common.Interfaces;

public record PlateOcrResult(string RecognizedText, string NormalizedText, double Confidence);

/// <summary>One corner of the plate region in the ORIGINAL photo's natural
/// pixel space (not display/screen space) — the user drags these four points
/// onto the plate's actual corners, in order TopLeft, TopRight,
/// BottomRight, BottomLeft, same order the perspective-warp math expects.</summary>
public record PlatePoint(double X, double Y);

/// <summary>OCR assist for the Vehicle Security scan flow — recognizes a
/// registration number by perspective-correcting the plate region the user
/// marked (their four corner points) out of the full photo, then running
/// OCR on the corrected image. Purely advisory: the caller always keeps the
/// result editable, never authoritative — see VehicleScanFeature.cs.</summary>
public interface IVehiclePlateOcrService
{
    Task<PlateOcrResult> RecognizeAsync(byte[] fullImageBytes, IReadOnlyList<PlatePoint> corners, CancellationToken ct = default);
}
