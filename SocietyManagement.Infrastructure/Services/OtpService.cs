using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher; // reused to hash the OTP at rest
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly IDateTime _dateTime;

    public OtpService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ISmsService smsService,
        IDateTime dateTime)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _smsService = smsService;
        _dateTime = dateTime;
    }

    public async Task<string> GenerateAndSendAsync(string destination, OtpPurpose purpose, int? userId, CancellationToken ct)
    {
        var code = System.Security.Cryptography.RandomNumberGenerator
            .GetInt32(0, (int)Math.Pow(10, AppConstants.OtpLengthDigits))
            .ToString($"D{AppConstants.OtpLengthDigits}");

        var otp = new OtpVerification
        {
            Destination = destination,
            CodeHash = _passwordHasher.Hash(code),
            Purpose = purpose,
            ExpiresAt = _dateTime.UtcNow.AddMinutes(AppConstants.OtpExpiryMinutes),
            UserId = userId
        };
        await _context.OtpVerifications.AddAsync(otp, ct);
        await _context.SaveChangesAsync(ct);

        var message = $"Your Society Management verification code is {code}. It expires in " +
                       $"{AppConstants.OtpExpiryMinutes} minutes. Do not share this code with anyone.";

        if (destination.IsEmailFormat())
        {
            await _emailService.SendEmailAsync(destination, "Your verification code", $"<p>{message}</p>", ct);
        }
        else
        {
            await _smsService.SendSmsAsync(destination, message, ct);
        }

        return code; // callers must NOT return this to the API response outside non-prod logging
    }

    public async Task<bool> ValidateAsync(string destination, OtpPurpose purpose, string code, CancellationToken ct)
    {
        var otp = await _context.OtpVerifications
            .Where(o => o.Destination == destination && o.Purpose == purpose && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null || otp.ExpiresAt < _dateTime.UtcNow)
        {
            return false;
        }

        if (otp.AttemptCount >= AppConstants.OtpMaxAttempts)
        {
            return false;
        }

        otp.AttemptCount++;

        var isValid = _passwordHasher.Verify(code, otp.CodeHash);
        if (isValid)
        {
            otp.IsUsed = true;
        }

        await _context.SaveChangesAsync(ct);
        return isValid;
    }
}
