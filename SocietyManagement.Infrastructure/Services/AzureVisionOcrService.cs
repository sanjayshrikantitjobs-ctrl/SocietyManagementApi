using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Cloud OCR for the Vehicle Security scan flow — an opt-in alternative, kept in
/// the codebase but not the default (see DependencyInjection.cs): the user
/// explicitly chose to stay free/local, so PaddleOcrVehicleOcrService is the
/// real primary provider. This activates only if VisionOcr:Endpoint/ApiKey are
/// configured.
///
/// Why Tesseract (and any classical detection heuristic in front of it) isn't
/// good enough: it's a document-OCR engine — trained/tuned for scanned pages of
/// clean, upright text. A phone-camera photo of a vehicle plate is scene text:
/// small, at an angle, with glare/shadow/background clutter — exactly the case
/// Tesseract performs worst at. Confirmed live across several rounds this
/// session: even with an automatic plate-region locator cropping tightly to the
/// plate first, Tesseract still returned garbled text. Azure AI Vision's Image
/// Analysis "Read" feature is trained specifically for real-world scene text
/// (it's the same OCR Microsoft ships for street signs, storefronts, etc.) and
/// does its own text detection internally, so no crop/locate step is needed
/// here at all — the whole photo is sent as-is.
///
/// The API returns every text line it finds in the photo (which, for a vehicle
/// photo, can include brand badges, dealer stickers, etc. alongside the actual
/// plate) — <see cref="PlateTextLineScorer.PickBestPlateLine"/> (shared with
/// PaddleOcrVehicleOcrService, since both providers return the same multi-line
/// shape) scores each line by how plate-shaped its alphanumeric content is and
/// returns that single best line as RawText, so nothing downstream
/// (VehicleNumberNormalizer, the confirm/edit UI, DB matching) needs to know
/// multiple candidates existed.
/// </summary>
public class AzureVisionOcrService : IVehicleOcrService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureVisionOcrService> _logger;

    public AzureVisionOcrService(HttpClient httpClient, IConfiguration configuration, ILogger<AzureVisionOcrService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<VehicleOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        try
        {
            var endpoint = _configuration["VisionOcr:Endpoint"]!.TrimEnd('/');
            var apiVersion = _configuration["VisionOcr:ApiVersion"] ?? "2024-02-01";

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{endpoint}/computervision/imageanalysis:analyze?api-version={apiVersion}&features=read");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _configuration["VisionOcr:ApiKey"]);
            request.Content = new ByteArrayContent(imageBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Vehicle OCR: Azure Vision request failed ({Status}): {Body}", response.StatusCode, body);
                return new VehicleOcrResult(Success: false, RawText: null, Confidence: 0, ErrorMessage: $"Azure Vision returned {response.StatusCode}");
            }

            var lines = ParseLines(body);
            var best = PlateTextLineScorer.PickBestPlateLine(lines);

            if (best is null)
            {
                _logger.LogInformation("Vehicle OCR: Azure Vision found no plate-shaped text line in the photo.");
                return new VehicleOcrResult(Success: true, RawText: string.Empty, Confidence: 0, ErrorMessage: null);
            }

            _logger.LogInformation(
                "Vehicle OCR: Azure Vision picked line {Text} (confidence {Confidence}) out of {Count} detected line(s).",
                best.Value.Text, best.Value.Confidence, lines.Count);

            return new VehicleOcrResult(Success: true, RawText: best.Value.Text, Confidence: best.Value.Confidence, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vehicle OCR: Azure Vision call failed.");
            return new VehicleOcrResult(Success: false, RawText: null, Confidence: 0, ErrorMessage: ex.Message);
        }
    }

    private static List<(string Text, double Confidence)> ParseLines(string responseBody)
    {
        var result = new List<(string, double)>();

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("readResult", out var readResult)) return result;
        if (!readResult.TryGetProperty("blocks", out var blocks)) return result;

        foreach (var block in blocks.EnumerateArray())
        {
            if (!block.TryGetProperty("lines", out var lines)) continue;

            foreach (var line in lines.EnumerateArray())
            {
                var text = line.GetProperty("text").GetString() ?? string.Empty;

                double confidence = 0;
                int wordCount = 0;
                if (line.TryGetProperty("words", out var words))
                {
                    foreach (var word in words.EnumerateArray())
                    {
                        if (word.TryGetProperty("confidence", out var confProp))
                        {
                            confidence += confProp.GetDouble();
                            wordCount++;
                        }
                    }
                }

                result.Add((text, wordCount > 0 ? confidence / wordCount : 0));
            }
        }

        return result;
    }
}
