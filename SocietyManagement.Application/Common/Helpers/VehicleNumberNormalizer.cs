namespace SocietyManagement.Application.Common.Helpers;

/// <summary>Collapses every cosmetic variation of an Indian vehicle
/// registration number ("MH 04 AB 1234", "mh-04-ab-1234") down to one
/// canonical uppercase alphanumeric form ("MH04AB1234") so OCR output,
/// user-typed search terms, and stored Vehicle.RegistrationNumber values
/// can all be compared directly.</summary>
public static class VehicleNumberNormalizer
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var chars = raw.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray();
        return new string(chars);
    }
}
