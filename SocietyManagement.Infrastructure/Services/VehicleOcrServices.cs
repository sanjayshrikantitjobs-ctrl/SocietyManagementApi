using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Stub implementation of IVehicleOcrService — logs instead of calling a real
/// provider, so the scan flow works end-to-end (camera capture, confirm/edit,
/// search, history) without OCR credentials. Always returns a successful read
/// with empty text and zero confidence, which deliberately forces every scan
/// through the manual confirm/edit step the spec requires for low-confidence
/// results — that's the one behavior a caller can rely on regardless of which
/// real provider (Azure AI Vision, Google Vision, a plate-recognition API)
/// eventually replaces this. Swap the body for a real HTTP call to that
/// provider — the interface (Application.Common.Interfaces) doesn't change,
/// only DependencyInjection.cs's registration and appsettings.json need
/// updating, same pattern as SmtpEmailService/StubSmsService/StubWhatsAppService.
/// </summary>
public class StubVehicleOcrService : IVehicleOcrService
{
    private readonly ILogger<StubVehicleOcrService> _logger;

    public StubVehicleOcrService(ILogger<StubVehicleOcrService> logger) => _logger = logger;

    public Task<VehicleOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        _logger.LogInformation("[VEHICLE OCR STUB] Received {Size} bytes — no provider configured, returning an empty low-confidence read.", imageBytes.Length);
        return Task.FromResult(new VehicleOcrResult(Success: true, RawText: "", Confidence: 0, ErrorMessage: null));
    }
}
