using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Real WhatsApp Business Cloud API implementation of IWhatsAppService, replacing
/// StubWhatsAppService once WhatsApp:AccessToken/PhoneNumberId are configured (see
/// DependencyInjection.cs). Calls Meta's Graph API directly — no third-party SDK.
///
/// WhatsApp's platform rule: a business can send a free-form text/document message
/// only within 24h of the customer's last message to it ("customer service window").
/// Outside that window, only an approved message template can be sent. Both public
/// methods here try the direct (non-template) send first — which is what actually
/// succeeds during a live session, and is exactly how we test this end-to-end
/// without waiting on template approval (message the business number once from the
/// recipient's phone to open the window) — and fall back to the template path only
/// if the direct attempt fails, which is the correct behavior for real proactive
/// notifications sent outside any session, once a template exists.
///
/// TextTemplateName defaults to "hello_world" — the one template every WhatsApp
/// Business Account has pre-approved with zero setup, so the template fallback path
/// itself is verifiable immediately even before a real template is approved. It
/// takes no parameters, so the caller's `message` text only actually reaches the
/// recipient via this fallback once TextTemplateName is switched to a real approved
/// template with a body {{1}} placeholder — within a session, the direct send above
/// already delivers the real text regardless.
///
/// DocumentTemplateName has no default — until a document-header template is
/// created and approved, the template-fallback path logs a warning and no-ops
/// rather than sending a request guaranteed to fail (the direct-send path above is
/// unaffected by this and still works within a session).
/// </summary>
public class WhatsAppBusinessApiService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppBusinessApiService> _logger;

    public WhatsAppBusinessApiService(HttpClient httpClient, IConfiguration configuration, ILogger<WhatsAppBusinessApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendWhatsAppAsync(string mobileNumber, string message, CancellationToken ct = default)
    {
        var to = ToE164(mobileNumber);

        var directPayload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body = message }
        };
        if (await PostMessageAsync(directPayload, "direct text", ct)) return;

        var templateName = _configuration["WhatsApp:TextTemplateName"] ?? "hello_world";
        var language = _configuration["WhatsApp:TextTemplateLanguage"] ?? "en_US";

        // hello_world takes zero parameters — sending a components array against
        // it is itself a Meta API error, so it's only included for a real template.
        object[]? components = templateName == "hello_world"
            ? null
            : new object[] { new { type = "body", parameters = new object[] { new { type = "text", text = message } } } };

        var templatePayload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = components == null
                ? new { name = templateName, language = new { code = language } }
                : (object)new { name = templateName, language = new { code = language }, components }
        };

        await PostMessageAsync(templatePayload, "template text fallback", ct);
    }

    public async Task SendWhatsAppDocumentAsync(
        string mobileNumber, string caption, byte[] documentBytes, string fileName, CancellationToken ct = default)
    {
        var to = ToE164(mobileNumber);
        var mediaId = await UploadMediaAsync(documentBytes, fileName, ct);
        if (mediaId == null) return;

        var directPayload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "document",
            document = new { id = mediaId, filename = fileName, caption }
        };
        if (await PostMessageAsync(directPayload, "direct document", ct)) return;

        var templateName = _configuration["WhatsApp:DocumentTemplateName"];
        if (string.IsNullOrWhiteSpace(templateName))
        {
            _logger.LogWarning(
                "Direct WhatsApp document send failed and WhatsApp:DocumentTemplateName is not configured — " +
                "cannot fall back for {FileName} to {Mobile}. This is expected outside a 24h customer service " +
                "window until a document-header template is created and approved.",
                fileName, mobileNumber);
            return;
        }

        var language = _configuration["WhatsApp:DocumentTemplateLanguage"] ?? "en_US";
        var templatePayload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = language },
                components = new object[]
                {
                    new { type = "header", parameters = new object[] { new { type = "document", document = new { id = mediaId, filename = fileName } } } },
                    new { type = "body", parameters = new object[] { new { type = "text", text = caption } } }
                }
            }
        };

        await PostMessageAsync(templatePayload, "template document fallback", ct);
    }

    private async Task<string?> UploadMediaAsync(byte[] content, string fileName, CancellationToken ct)
    {
        var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
        var apiVersion = _configuration["WhatsApp:ApiVersion"] ?? "v21.0";

        using var form = new MultipartFormDataContent
        {
            { new StringContent("whatsapp"), "messaging_product" },
            { new StringContent("application/pdf"), "type" }
        };
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", fileName);

        var url = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/media";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["WhatsApp:AccessToken"]);

        _logger.LogInformation("WhatsApp media upload: POST {Url}", url);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("WhatsApp media upload failed ({Status}): {Body}", response.StatusCode, body);
            return null;
        }

        return JsonSerializer.Deserialize<MediaUploadResponse>(body)?.Id;
    }

    /// <summary>Returns true on success, false on failure — callers use this to
    /// decide whether to attempt a template fallback. Deliberately never throws:
    /// a WhatsApp delivery failure shouldn't fail the caller's whole operation
    /// (e.g. bill generation) the way an email/DB failure would.</summary>
    private async Task<bool> PostMessageAsync(object payload, string attemptLabel, CancellationToken ct)
    {
        var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
        var apiVersion = _configuration["WhatsApp:ApiVersion"] ?? "v21.0";
        var accessToken = _configuration["WhatsApp:AccessToken"];
        var url = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Logged deliberately every attempt, not just on failure: the response body
        // alone (a real message ID, HTTP 200) was indistinguishable between the old
        // test number and the new production number in prior debugging — only the
        // request URL/token actually prove which credentials were used. Token is
        // truncated, never logged in full.
        var tokenSuffix = accessToken?.Length > 6 ? accessToken[^6..] : accessToken;
        _logger.LogInformation("WhatsApp {Attempt} send: POST {Url} using token ending in ...{TokenSuffix}", attemptLabel, url, tokenSuffix);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WhatsApp {Attempt} send failed ({Status}): {Body}", attemptLabel, response.StatusCode, body);
            return false;
        }

        _logger.LogInformation("WhatsApp {Attempt} send succeeded. Response: {Body}", attemptLabel, body);
        return true;
    }

    /// <summary>WhatsApp requires a full E.164 number without a leading '+'.
    /// This app stores mobile numbers as plain 10-digit Indian numbers, so
    /// prepend WhatsApp:DefaultCountryCode unless one's already present.</summary>
    private string ToE164(string mobileNumber)
    {
        var digitsOnly = new string(mobileNumber.Where(char.IsDigit).ToArray());
        var countryCode = _configuration["WhatsApp:DefaultCountryCode"] ?? "91";
        return digitsOnly.Length == 10 ? countryCode + digitsOnly : digitsOnly;
    }

    private class MediaUploadResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
