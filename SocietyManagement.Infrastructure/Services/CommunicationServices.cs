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

    public Task SendWhatsAppDocumentAsync(
        string mobileNumber, string caption, byte[] documentBytes, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[WHATSAPP STUB] To: {Mobile} | Caption: {Caption} | Attachment: {FileName} ({Size} bytes)",
            mobileNumber, caption, fileName, documentBytes.Length);
        return Task.CompletedTask;
    }
}
