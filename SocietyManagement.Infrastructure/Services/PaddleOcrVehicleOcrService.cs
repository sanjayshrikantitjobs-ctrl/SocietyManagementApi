using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Real open-source OCR for the Vehicle Security scan flow, replacing
/// TesseractVehicleOcrService as the primary local provider (see
/// DependencyInjection.cs). Uses PaddleOCR (Apache-2.0, via the actively
/// maintained `Sdcb.PaddleOCR` .NET wrapper) instead of Tesseract.
///
/// Why the switch: Tesseract is a document/book-text OCR engine. Even with a
/// custom plate-region locator built and iteratively debugged this session
/// (Otsu threshold + connected components + a character-blob content check),
/// and even handed a verified-correct, tightly-cropped plate photo, Tesseract
/// still misread it — confirmed across multiple preprocessing variants and
/// both official trained-data releases (fast/best). PP-OCR's models are
/// trained on diverse real-world scene text (signs, receipts, street photos),
/// a much closer match to a photographed plate, and — critically — the
/// pipeline does its own text DETECTION internally, so no manual crop/locate
/// step is needed at all: the whole photo goes in, and the model finds and
/// reads the plate itself.
///
/// Lifetime: this class is registered as a singleton (see
/// DependencyInjection.cs), unlike TesseractVehicleOcrService's deliberate
/// per-call construction — PaddleOcrAll loads model weights into native
/// memory on construction, so building one per request would be a severe
/// latency/memory regression. Concurrent Run() calls aren't documented as
/// thread-safe, so calls are serialized through a semaphore rather than
/// assumed safe — cheap insurance at this app's expected gate-scan volume.
/// </summary>
public sealed class PaddleOcrVehicleOcrService : IVehicleOcrService, IDisposable
{
    private readonly PaddleOcrAll _engine;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<PaddleOcrVehicleOcrService> _logger;

    public PaddleOcrVehicleOcrService(ILogger<PaddleOcrVehicleOcrService> logger)
    {
        _logger = logger;
        _engine = new PaddleOcrAll(LocalFullModels.EnglishV5, PaddleDevice.Blas())
        {
            AllowRotateDetection = true,
            Enable180Classification = false,
        };
    }

    public async Task<VehicleOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        try
        {
            using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);

            await _gate.WaitAsync(ct);
            try
            {
                var result = _engine.Run(mat);
                var lines = result.Regions.Select(r => (r.Text, (double)r.Score)).ToList();
                var best = PlateTextLineScorer.PickBestPlateLine(lines);

                if (best is null)
                {
                    _logger.LogInformation("Vehicle OCR: PaddleOCR found no plate-shaped text line out of {Count} detected line(s).", lines.Count);
                    return new VehicleOcrResult(Success: true, RawText: string.Empty, Confidence: 0, ErrorMessage: null);
                }

                _logger.LogInformation(
                    "Vehicle OCR: PaddleOCR picked line {Text} (confidence {Confidence}) out of {Count} detected line(s).",
                    best.Value.Text, best.Value.Confidence, lines.Count);

                return new VehicleOcrResult(Success: true, RawText: best.Value.Text, Confidence: best.Value.Confidence, ErrorMessage: null);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vehicle OCR: PaddleOCR recognition failed.");
            return new VehicleOcrResult(Success: false, RawText: null, Confidence: 0, ErrorMessage: ex.Message);
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
        _gate.Dispose();
    }
}
