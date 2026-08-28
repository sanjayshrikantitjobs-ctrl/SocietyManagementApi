namespace SocietyManagement.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>SMS-Ready per spec: interface exists now, wired to a stub logger
/// implementation; swap in Twilio/MSG91/etc. in Infrastructure DI without touching
/// any Application code.</summary>
public interface ISmsService
{
    Task SendSmsAsync(string mobileNumber, string message, CancellationToken ct = default);
}

/// <summary>WhatsApp-Ready per spec, same stub-now/swap-later pattern as ISmsService.
/// The provider-abstraction seam for Maintenance bill delivery: swap the
/// Infrastructure implementation for WhatsApp Business API/Twilio/Interakt/WATI
/// without touching any Application code.</summary>
public interface IWhatsAppService
{
    Task SendWhatsAppAsync(string mobileNumber, string message, CancellationToken ct = default);

    /// <summary>Sends a message with a document attachment (e.g. a bill PDF).</summary>
    Task SendWhatsAppDocumentAsync(
        string mobileNumber, string caption, byte[] documentBytes, string fileName, CancellationToken ct = default);

    /// <summary>Sends a message with an image, referenced by a publicly-reachable
    /// URL (e.g. a visitor photo already sitting in blob storage) rather than raw
    /// bytes — avoids a redundant download-then-reupload round trip.</summary>
    Task SendWhatsAppImageAsync(string mobileNumber, string caption, string imageUrl, CancellationToken ct = default);
}
