using System.Globalization;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Shared.Converters;

public class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Inverse of StringToBoolConverter — true when the bound string is
/// null/blank. Used to flip between a photo and its fallback placeholder
/// without needing converter chaining, which single MAUI Bindings don't support.</summary>
public class StringIsNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}

public class CountToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Generic "is this bound value present" check — works uniformly
/// for a nullable reference (a DTO), a nullable value type (int?), or a
/// string (unlike StringToBoolConverter, an empty string here still counts
/// as "present" — use StringToBoolConverter instead where blank should mean
/// "nothing to show").</summary>
public class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value != null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Generic "does this bound value equal ConverterParameter" check —
/// used to drive a segmented tab switcher's per-panel IsVisible off one
/// SelectedTab string property (compares as strings so it works for enum,
/// int, or string bound values alike).</summary>
public class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.Equals(parameter?.ToString(), StringComparison.OrdinalIgnoreCase) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class IsNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value == null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Mirrors the web's AssetUrlPipe/resolveAssetUrl — the API's file
/// upload endpoints return every stored file as a relative path (e.g.
/// "/uploads/visitors/xyz.jpg"), correct for the API to store but not
/// directly usable as an Image.Source, which needs an absolute URL.
/// Resolves it against ApiConfig.ApiBaseUrl at bind time; an already-absolute
/// URL (http/https/data/blob) passes through unchanged. Returns a real
/// ImageSource (not a bare string) — binding Image.Source through a value
/// converter skips the XAML-literal string->ImageSource coercion, so handing
/// back a plain string here can throw at runtime instead of just rendering.</summary>
public class AssetUrlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("data:") || url.StartsWith("blob:")) return null;

        var absolute = url.StartsWith("http://") || url.StartsWith("https://")
            ? url
            : ApiConfig.ApiBaseUrl.TrimEnd('/') + url;

        return Uri.TryCreate(absolute, UriKind.Absolute, out var uri) ? ImageSource.FromUri(uri) : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
