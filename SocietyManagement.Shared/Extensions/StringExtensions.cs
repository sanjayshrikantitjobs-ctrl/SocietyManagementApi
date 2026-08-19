using System.Text.RegularExpressions;

namespace SocietyManagement.Shared.Extensions;

public static class StringExtensions
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MobileRegex = new(@"^[6-9]\d{9}$", RegexOptions.Compiled);

    public static bool IsValidEmail(this string value) => EmailRegex.IsMatch(value);

    public static bool IsValidIndianMobile(this string value) => MobileRegex.IsMatch(value);

    public static bool IsEmailFormat(this string identifier) => identifier.Contains('@');
}
