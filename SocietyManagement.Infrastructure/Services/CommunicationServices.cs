using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Stub implementations for email/SMS/WhatsApp. They log instead of dispatching so
/// the whole system works end-to-end without external provider credentials.
/// Swap the body for SendGrid/SMTP, Twilio/MSG91, and the WhatsApp Business API
/// respectively — the interfaces (Application.Common.Interfaces) don't change, only
/// DependencyInjection.cs's registration and appsettings.json need updating.
/// This satisfies the spec's "SMS Ready" / "WhatsApp Ready" requirement: the
/// integration seam exists now, delivery is pluggable later.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ILogger<SmtpEmailService> logger) => _logger = logger;

    public Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL STUB] To: {To} | Subject: {Subject} | Body: {Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}

public class StubSmsService : ISmsService
{
    private readonly ILogger<StubSmsService> _logger;

    public StubSmsService(ILogger<StubSmsService> logger) => _logger = logger;

    public Task SendSmsAsync(string mobileNumber, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("[SMS STUB] To: {Mobile} | Message: {Message}", mobileNumber, message);
        return Task.CompletedTask;
    }
}

public class StubWhatsAppService : IWhatsAppService
{
    private readonly ILogger<StubWhatsAppService> _logger;

    public StubWhatsAppService(ILogger<StubWhatsAppService> logger) => _logger = logger;

    public Task SendWhatsAppAsync(string mobileNumber, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("[WHATSAPP STUB] To: {Mobile} | Message: {Message}", mobileNumber, message);
        return Task.CompletedTask;
    }

    public Task<bool> SendWhatsAppDocumentAsync(
        string mobileNumber, string caption, byte[] documentBytes, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[WHATSAPP STUB] To: {Mobile} | Caption: {Caption} | Attachment: {FileName} ({Size} bytes)",
            mobileNumber, caption, fileName, documentBytes.Length);
        return Task.FromResult(true);
    }

    public Task SendWhatsAppImageAsync(string mobileNumber, string caption, string imageUrl, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[WHATSAPP STUB] To: {Mobile} | Caption: {Caption} | Image: {ImageUrl}",
            mobileNumber, caption, imageUrl);
        return Task.CompletedTask;
    }

    public Task<bool> SendWhatsAppTemplateAsync(
        string mobileNumber, string templateName, string languageCode, IReadOnlyList<string> bodyParameters, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[WHATSAPP STUB] To: {Mobile} | Template: {Template} ({Language}) | Params: {Params}",
            mobileNumber, templateName, languageCode, string.Join(", ", bodyParameters));
        return Task.FromResult(true);
    }

    public Task<bool> SendWhatsAppDocumentTemplateAsync(
        string mobileNumber, string templateName, string languageCode, IReadOnlyList<string> bodyParameters,
        byte[] documentBytes, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[WHATSAPP STUB] To: {Mobile} | Document Template: {Template} ({Language}) | Params: {Params} | Attachment: {FileName} ({Size} bytes)",
            mobileNumber, templateName, languageCode, string.Join(", ", bodyParameters), fileName, documentBytes.Length);
        return Task.FromResult(true);
    }

    public Task<bool> SendWhatsAppImageTemplateAsync(
        string mobileNumber, string templateName, string languageCode, IReadOnlyList<string> bodyParameters,
        string imageUrl, IReadOnlyList<string> buttonUrlParameters, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[WHATSAPP STUB] To: {Mobile} | Image Template: {Template} ({Language}) | Params: {Params} | Image: {ImageUrl} | Button params: {ButtonParams}",
            mobileNumber, templateName, languageCode, string.Join(", ", bodyParameters), imageUrl, string.Join(", ", buttonUrlParameters));
        return Task.FromResult(true);
    }
}
