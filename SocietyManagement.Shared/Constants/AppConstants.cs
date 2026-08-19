namespace SocietyManagement.Shared.Constants;

public static class AppConstants
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxFailedLoginAttempts = 5;
    public const int AccountLockoutMinutes = 15;
    public const int OtpLengthDigits = 6;
    public const int OtpExpiryMinutes = 5;
    public const int OtpMaxAttempts = 3;
    public const int AccessTokenExpiryMinutes = 15;
    public const int RefreshTokenExpiryDays = 7;
    public const int PasswordResetTokenExpiryMinutes = 30;
}
