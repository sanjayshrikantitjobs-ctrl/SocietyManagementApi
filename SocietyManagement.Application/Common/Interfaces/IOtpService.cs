using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Common.Interfaces;

public interface IOtpService
{
    /// <summary>Generates a numeric OTP, hashes+persists it, dispatches via
    /// IEmailService/ISmsService depending on whether destination looks like an
    /// email or a mobile number, and returns the plaintext code only for logging
    /// in non-production environments (never returned to the API response).</summary>
    Task<string> GenerateAndSendAsync(string destination, OtpPurpose purpose, int? userId, CancellationToken ct);

    Task<bool> ValidateAsync(string destination, OtpPurpose purpose, string code, CancellationToken ct);
}
