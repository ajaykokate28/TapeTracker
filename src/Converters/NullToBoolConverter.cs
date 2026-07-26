using System.Globalization;

namespace TapeTracker.Converters;

/// <summary>
/// Returns <c>true</c> when the value is non-null and (for strings) non-empty/non-whitespace.
/// Useful for collapsing UI elements bound to optional string fields such as <c>Customer.Tag</c>.
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return false;
        if (value is string s) return !string.IsNullOrWhiteSpace(s);
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
