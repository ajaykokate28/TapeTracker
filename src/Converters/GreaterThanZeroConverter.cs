using System.Globalization;

namespace TapeTracker.Converters;

/// <summary>
/// Returns <c>true</c> when the bound value is a positive number (int, long, double, etc.).
/// Useful for collapsing rows that are bound to Count-style properties when the collection is empty.
/// </summary>
public class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return false;
        try
        {
            var d = System.Convert.ToDouble(value, culture);
            return d > 0;
        }
        catch
        {
            return false;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
