using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>
/// Short-lived one-time-passcode used for login OTP, forgot-password OTP,
/// mobile/email verification. Generic on <see cref="Purpose"/> so one table
/// backs every OTP flow instead of one table per feature.
/// </summary>
public class OtpVerification : BaseAuditableEntity
{
    public string Destination { get; set; } = default!; // email or mobile number

    public string CodeHash { get; set; } = default!; // OTP is hashed at rest, never stored plain

    public OtpPurpose Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int AttemptCount { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }
}
