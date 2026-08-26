namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Result of a single plate-recognition attempt. RawText is
/// whatever the provider read verbatim (before VehicleNumberNormalizer);
/// Confidence is 0-1. Success=false means the provider itself errored
/// (network/quota/etc), distinct from Success=true with a low Confidence
/// (the provider read *something*, just isn't sure of it) — the caller
/// always routes both cases through the same confirm/edit UI.</summary>
public record VehicleOcrResult(bool Success, string? RawText, double Confidence, string? ErrorMessage);

/// <summary>Abstraction over the plate-recognition provider so Application
/// handlers never depend on which one is wired up. Implemented today by
/// Infrastructure.Services.StubVehicleOcrService (always a low-confidence
/// empty read — forces the manual confirm/edit path); swap for Azure AI
/// Vision / Google Vision / a dedicated plate-recognition API later
/// without touching any handler.</summary>
public interface IVehicleOcrService
{
    Task<VehicleOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default);
}
