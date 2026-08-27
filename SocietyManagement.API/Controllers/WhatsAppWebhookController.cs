using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SocietyManagement.API.Controllers;

/// <summary>
/// Receives Meta's WhatsApp Cloud API webhook callbacks — the only way to see
/// real delivery/failure status for a sent message. The synchronous send
/// response (see WhatsAppBusinessApiService.cs) only confirms Meta *accepted*
/// a message into its queue; whether it was actually delivered, read, or
/// failed (and why) is pushed here asynchronously, sometimes seconds to
/// minutes later.
///
/// Two endpoints, both required by Meta's webhook contract:
///   - GET: the one-time verification handshake Meta performs when you save
///     the callback URL in the developer dashboard. Must echo back
///     hub.challenge if hub.verify_token matches WhatsApp:WebhookVerifyToken
///     (a value you make up and enter in both places — see DependencyInjection
///     comments / appsettings for where it's configured).
///   - POST: the actual event delivery — message status changes
///     (sent/delivered/read/failed) and incoming messages. Currently just
///     logs everything at Information level for visibility; not yet
///     persisted to a table, since the immediate goal is diagnosing why
///     sends weren't arriving, not building a full delivery-tracking feature.
///
/// [AllowAnonymous] on both — Meta calls this endpoint directly, with no
/// knowledge of this app's own JWT auth. The verify token (GET) and optional
/// signature check (POST, if WhatsApp:AppSecret is configured) are what
/// actually authenticate the caller as really being Meta.
/// </summary>
[ApiController]
[Route("api/webhooks/whatsapp")]
[AllowAnonymous]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(IConfiguration configuration, ILogger<WhatsAppWebhookController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expectedToken = _configuration["WhatsApp:WebhookVerifyToken"];

        if (mode == "subscribe" && !string.IsNullOrEmpty(expectedToken) && verifyToken == expectedToken)
        {
            _logger.LogInformation("WhatsApp webhook verification succeeded.");
            return Content(challenge ?? string.Empty, "text/plain");
        }

        _logger.LogWarning("WhatsApp webhook verification failed: mode={Mode}, token matched={Matched}", mode, verifyToken == expectedToken);
        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var appSecret = _configuration["WhatsApp:AppSecret"];
        if (!string.IsNullOrWhiteSpace(appSecret) && !IsValidSignature(rawBody, appSecret))
        {
            _logger.LogWarning("WhatsApp webhook payload rejected: signature validation failed.");
            return Unauthorized();
        }

        // Full payload logged verbatim for now — this is the actual evidence
        // needed to see why a send didn't arrive (a "failed" status with a
        // specific error code/message, a "delivered" status proving it did
        // arrive and something else is wrong, etc.).
        _logger.LogInformation("WhatsApp webhook received: {Payload}", rawBody);

        return Ok();
    }

    /// <summary>Meta signs the raw POST body with HMAC-SHA256 using the app's
    /// secret (Meta App Dashboard -> Settings -> Basic -> App Secret — not the
    /// same as the WhatsApp access token), sent as
    /// "X-Hub-Signature-256: sha256=&lt;hex&gt;". Verifying this confirms the
    /// request genuinely came from Meta. Only enforced when
    /// WhatsApp:AppSecret is configured, so basic webhook logging works
    /// immediately without needing that value first.</summary>
    private bool IsValidSignature(string rawBody, string appSecret)
    {
        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader)) return false;

        var expectedHex = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

        var providedHex = signatureHeader.ToString().Replace("sha256=", string.Empty, StringComparison.OrdinalIgnoreCase);

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedHex), Encoding.UTF8.GetBytes(providedHex));
    }
}
