using System.Globalization;

namespace TapeTracker.Converters;

/// <summary>
/// Converts a full name string into 1–2 uppercase initials (e.g. "John Doe" → "JD").
/// </summary>
public class InitialsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                : name[0].ToString().ToUpper();
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
